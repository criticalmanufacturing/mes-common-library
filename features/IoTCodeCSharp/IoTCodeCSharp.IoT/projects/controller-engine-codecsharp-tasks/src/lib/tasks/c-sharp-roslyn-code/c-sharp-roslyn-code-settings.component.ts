import { CommonModule } from "@angular/common";
import { Component, NgModule, OnDestroy, ViewContainerRef } from "@angular/core";
import { PropertyContainerModule, PropertyEditorModule } from "cmf-core-business-controls";
import { TaskDefinitionSettings, TaskSettingsBase, TaskSettingsModule, TaskSettingsService } from "cmf-core-connect-iot";
import { BaseWidgetModule, CodeEditorLanguage, CodeEditorModule, MonacoLoaderService } from "cmf-core-controls";
import { CSharpRoslynCodeSettings as CSharpRoslynCodeProperties, ROSLYN_SETTINGS_DEFAULTS } from "./c-sharp-roslyn-code.task";
import { CSHARP_ROSLYN_TEMPLATE_CONTENT } from "./c-sharp-roslyn-code-template";
import { RoslynCompilerBrowser } from "./roslyn/roslynCompilerBrowser";

export interface CSharpRoslynCodeTaskSettings extends CSharpRoslynCodeProperties, TaskDefinitionSettings { }

// Roslyn's own completion item tag names -> monaco.languages.CompletionItemKind member names.
// Anything not listed here (e.g. "Snippet", "Local", "Parameter") falls back to "Text".
const ROSLYN_KIND_TO_MONACO_KIND: Record<string, string> = {
    Class: "Class",
    Structure: "Struct",
    Interface: "Interface",
    Enum: "Enum",
    EnumMember: "EnumMember",
    Delegate: "Interface",
    Module: "Module",
    Namespace: "Module",
    Method: "Method",
    ExtensionMethod: "Method",
    Property: "Property",
    Field: "Field",
    Event: "Event",
    Constant: "Constant",
    Keyword: "Keyword",
    TypeParameter: "TypeParameter",
    Operator: "Operator"
};

let sharedCompletionDisposable: { dispose(): void } | undefined;
let sharedCompletionRefCount = 0;
const DIAGNOSTICS_OWNER = "csharp-roslyn";
const VALIDATION_DEBOUNCE_MS = 600;
const MONACO_TRIGGER_SUGGEST_COMMAND = "editor.action.triggerSuggest";

@Component({
    selector: "lib-csharp-roslyn-code-settings",
    templateUrl: "./c-sharp-roslyn-code-settings.component.html",
    styleUrls: ["./c-sharp-roslyn-code-settings.component.less"]
})
export class CSharpRoslynCodeSettings extends TaskSettingsBase implements OnDestroy {
    public CodeEditorLanguage = CodeEditorLanguage;
    public _source = "";
    private _sourceChange: string | undefined;
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    private _monacoEditor: any;
    private _validationTimeout: ReturnType<typeof setTimeout> | undefined;
    public override settings: CSharpRoslynCodeTaskSettings;

    constructor(
        viewContainerRef: ViewContainerRef,
        service: TaskSettingsService,
        private _compiler: RoslynCompilerBrowser,
        private _monacoLoader: MonacoLoaderService
    ) {
        super(viewContainerRef, service);
        service.onBeforeSave = this.onBeforeSave.bind(this);
    }

    public ngOnInit(): void {
        const currentSettings = Object.assign({}, this.settings);
        Object.assign(this.settings, ROSLYN_SETTINGS_DEFAULTS, currentSettings);
        this._source = this.settings.csCodeBase64
            ? atob(this.settings.csCodeBase64)
            : (this.settings.csCode ?? []).join("\n") || CSHARP_ROSLYN_TEMPLATE_CONTENT;
        this._registerFrameworkCompletions();
    }

    public ngOnDestroy(): void {
        if (--sharedCompletionRefCount === 0) {
            sharedCompletionDisposable?.dispose();
            sharedCompletionDisposable = undefined;
        }
        if (this._validationTimeout != null) {
            clearTimeout(this._validationTimeout);
        }
    }

    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    public onEditorChange(editor: any): void {
        this._monacoEditor = editor;
        this._scheduleValidation();
    }

    public onSourceValueChange(value: string): void {
        this._sourceChange = value;
        this._scheduleValidation();
        // Unlike VS Code, this editor doesn't reliably trigger suggestions on a "." trigger
        // character by itself — explicitly ask for them (mirrors PageDEEActionCode's same workaround).
        if (value.endsWith(".")) {
            this._monacoEditor?.trigger?.("", MONACO_TRIGGER_SUGGEST_COMMAND, {});
        }
    }

    private _scheduleValidation(): void {
        if (this._validationTimeout != null) {
            clearTimeout(this._validationTimeout);
        }
        this._validationTimeout = setTimeout(() => this._validate(), VALIDATION_DEBOUNCE_MS);
    }

    private async _validate(): Promise<void> {
        const model = this._monacoEditor?.getModel?.();
        if (model == null) { return; }
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        const monaco = (window as any).monaco;
        const source = this._sourceChange ?? this._source;
        try {
            const compilation = await this._compiler.compile(source);
            if (this._monacoEditor?.getModel?.() !== model) { return; }
            const markers = compilation.diagnostics.map(diagnostic => {
                const line = diagnostic.line ?? 1;
                const column = diagnostic.column ?? 1;
                return {
                    severity: diagnostic.severity === "error"
                        ? monaco.MarkerSeverity.Error
                        : diagnostic.severity === "warning"
                            ? monaco.MarkerSeverity.Warning
                            : monaco.MarkerSeverity.Info,
                    message: diagnostic.message,
                    startLineNumber: line,
                    startColumn: column,
                    endLineNumber: line,
                    endColumn: column + 1
                };
            });
            monaco.editor.setModelMarkers(model, DIAGNOSTICS_OWNER, markers);
        } catch {
            // Compilation failures unrelated to diagnostics (e.g. compiler not yet loaded) are ignored here.
        }
    }

    public async onBeforeSave(settings: CSharpRoslynCodeProperties): Promise<CSharpRoslynCodeProperties> {
        const source = this._sourceChange ?? this._source;
        const compilation = await this._compiler.compile(source);
        const errors = compilation.diagnostics.filter(diagnostic => diagnostic.severity === "error");
        if (errors.length > 0) {
            throw new Error(errors.map(error => error.message).join("\n"));
        }
        settings.csCode = source.replace(/\t/g, "    ").split("\n");
        settings.csCodeBase64 = btoa(source);
        settings.roslynAssemblyBase64 = compilation.assemblyBase64;
        settings.roslynSourceHash = compilation.sourceHash;
        settings.roslynCatalogVersion = compilation.catalogVersion;
        return settings;
    }

    // Delegates to Roslyn's real CompletionService (running in-browser via WASM, see
    // roslyn-compiler/CompilerInterop.cs) instead of a hand-written set of rules — same shape as
    // PageDEEActionCode's DEEActionLanguageService.completionProvider, except that one calls a
    // server-side LBO for a real Roslyn-backed result, and this calls our own in-browser one:
    // whichever framework/LBO/BCL member is actually valid at that point in the real compiled
    // source is what gets suggested, so this never goes stale relative to the language itself.
    private async _registerFrameworkCompletions(): Promise<void> {
        sharedCompletionRefCount++;
        if (sharedCompletionDisposable) { return; }
        await this._monacoLoader.waitForMonaco();
        if (sharedCompletionDisposable) { return; }
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        const monaco = (window as any).monaco;

        sharedCompletionDisposable = monaco.languages.registerCompletionItemProvider("csharp", {
            triggerCharacters: ["."],
            // eslint-disable-next-line @typescript-eslint/no-explicit-any
            provideCompletionItems: async (model: any, position: any) => {
                const word = model.getWordUntilPosition(position);
                const range = { startLineNumber: position.lineNumber, endLineNumber: position.lineNumber, startColumn: word.startColumn, endColumn: word.endColumn };
                const source = model.getValue();
                const offset = model.getOffsetAt(position);
                try {
                    const items = await this._compiler.getCompletions(source, offset);
                    return {
                        suggestions: items.map(item => ({
                            label: item.label,
                            kind: monaco.languages.CompletionItemKind[ROSLYN_KIND_TO_MONACO_KIND[item.kind] ?? "Text"],
                            insertText: item.label,
                            sortText: item.sortText,
                            range,
                            // Not part of Monaco's CompletionItem shape, but it round-trips whatever
                            // we attach back to resolveCompletionItem below unmodified — stashed here
                            // so the item's documentation can be resolved lazily, only for whichever
                            // one the user actually highlights, instead of for the whole list upfront.
                            _roslynSource: source,
                            _roslynPosition: offset
                        }))
                    };
                } catch (error) {
                    // Surfaced instead of swallowed: a silent failure here just means Monaco falls
                    // back to its own generic "words already in this document" suggestions, which
                    // looks like real completion working with most framework members missing rather
                    // than like a failure — very easy to misdiagnose without this.
                    // eslint-disable-next-line no-console
                    console.warn("C# Roslyn completion request failed:", error);
                    return { suggestions: [] };
                }
            },
            // eslint-disable-next-line @typescript-eslint/no-explicit-any
            resolveCompletionItem: async (item: any) => {
                try {
                    item.documentation = await this._compiler.getCompletionDescription(item._roslynSource, item._roslynPosition, item.label, item.sortText);
                } catch (error) {
                    // eslint-disable-next-line no-console
                    console.warn("C# Roslyn completion description request failed:", error);
                }
                return item;
            }
        });
    }
}

@NgModule({
    imports: [CommonModule, CodeEditorModule, TaskSettingsModule, BaseWidgetModule, PropertyContainerModule, PropertyEditorModule],
    declarations: [CSharpRoslynCodeSettings],
    providers: [RoslynCompilerBrowser]
})
export class CSharpRoslynCodeSettingsModule { }