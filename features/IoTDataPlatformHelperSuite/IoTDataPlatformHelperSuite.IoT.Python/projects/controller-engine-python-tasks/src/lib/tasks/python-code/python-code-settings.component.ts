import {
    CommonModule
} from "@angular/common";

import {
    NgModule,
    forwardRef,
    Component,
    OnInit,
    OnChanges,
    OnDestroy,
    ViewContainerRef,
    SimpleChanges
} from "@angular/core";

import {
    HOST_VIEW_COMPONENT
} from "cmf-core";

import {
    PropertyContainerModule,
    PropertyEditorModule
} from "cmf-core-business-controls";

import {
    TaskSettingsBase,
    TaskSettingsModule,
    TaskSettingsService,
    TaskDefinitionSettings
} from "cmf-core-connect-iot";

import {
    ValidatorModule,
    CodeEditorModule,
    CodeEditorLanguage,
    OnValidateArgs,
    ValidatorModel,
    OnValidate,
    BaseWidgetModule,
    PopOverInfoModule,
    MonacoLoaderService
} from "cmf-core-controls";

import {
    PythonCodeSettings as PythonCodeProperties,
    SETTINGS_DEFAULTS
} from "./python-code.task";

import { decode, encode } from "./utilities";
import { PYTHON_TEMPLATE_CONTENT } from "./python-code-template";
import { System } from "@criticalmanufacturing/connect-iot-controller-engine";

/** Constants */
export interface PythonCodeTaskSettings extends PythonCodeProperties, TaskDefinitionSettings { }

/** Module-level singleton — Monaco providers are global, so the registration must be too */
let _sharedCompletionDisposable: { dispose(): void } | undefined;
let _sharedCompletionRefCount = 0;

/**
 * Python code task settings component
 */
@Component({
    selector: "lib-python-code-settings",
    templateUrl: "./python-code-settings.component.html",
    styleUrls: ["./python-code-settings.component.less"],
    viewProviders: [{ provide: HOST_VIEW_COMPONENT, useExisting: forwardRef(() => PythonCodeSettings) }]
})
export class PythonCodeSettings extends TaskSettingsBase implements OnInit, OnChanges, OnDestroy, OnValidate {

    public CodeEditorLanguage = CodeEditorLanguage;

    public _pyCode: string;
    private _pyCodeChange: string;

    /** Task settings */
    public override settings: PythonCodeTaskSettings;

    constructor(
        viewContainerRef: ViewContainerRef,
        service: TaskSettingsService,
        private _monacoLoader: MonacoLoaderService
    ) {
        super(viewContainerRef, service);
        service.onBeforeSave = this.onBeforeSave.bind(this);
    }

    /** Triggered when the task is created — applies default values */
    public ngOnInit(): void {
        const currentSettings = Object.assign({}, this.settings);
        Object.assign(this.settings, SETTINGS_DEFAULTS, currentSettings);

        this._pyCode = decode(this.settings.pyCodeBase64 || "") || this.fromMultilineArray(this.settings.pyCode) || PYTHON_TEMPLATE_CONTENT;

        this.registerFrameworkCompletions();
    }

    public ngOnDestroy(): void {
        if (--_sharedCompletionRefCount === 0) {
            _sharedCompletionDisposable?.dispose();
            _sharedCompletionDisposable = undefined;
        }
    }
    /** Triggered when an element from the html page is changed */
    public override ngOnChanges(changes: SimpleChanges): void {
        super.ngOnChanges(changes);
    }

    /** When the code editor value changes */
    public onCodeEditorValueChange(value: string): void {
        this._pyCodeChange = value;
    }

    /** Validate the settings (Python has no transpile step) */
    public async onValidate(context: OnValidateArgs, model: ValidatorModel): Promise<boolean> {
        return model.validate(context);
    }

    /** Encode and persist the Python code before saving */
    public async onBeforeSave(settings: PythonCodeProperties): Promise<PythonCodeProperties> {
        if (this._pyCodeChange != null && this._pyCode !== this._pyCodeChange) {
            settings.pyCode = this.toMultilineArray(this._pyCodeChange);
            settings.pyCodeBase64 = encode(this._pyCodeChange);
        } else if (settings.pyCodeBase64) {
            const pyCode = decode(settings.pyCodeBase64);
            settings.pyCode = this.toMultilineArray(pyCode);
        }

        if (!Array.isArray(this.settings.packages)) {
            this.settings.packages = (this.settings.packages as string).split(",").map(pkg => pkg.trim()).filter(pkg => pkg.length > 0);
        }
        return settings;
    }

    private async registerFrameworkCompletions(): Promise<void> {
        _sharedCompletionRefCount++;
        if (_sharedCompletionDisposable) { return; }
        await this._monacoLoader.waitForMonaco();
        if (_sharedCompletionDisposable) { return; }  // another instance registered while awaiting
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        const monaco = (window as any).monaco;

        _sharedCompletionDisposable = monaco.languages.registerCompletionItemProvider("python", {
            triggerCharacters: [".", "'", "\""],
            provideCompletionItems: (model: any, position: any) => {
                const line: string = model.getLineContent(position.lineNumber);
                const before: string = line.substring(0, position.column - 1);
                const word = model.getWordUntilPosition(position);
                const range = {
                    startLineNumber: position.lineNumber, endLineNumber: position.lineNumber,
                    startColumn: word.startColumn, endColumn: word.endColumn
                };

                if (/self\.logger\.$/.test(before)) {
                    return {
                        suggestions: [
                            { label: "info", kind: monaco.languages.CompletionItemKind.Method, insertText: "info(${1:msg})", insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet, documentation: "Log an info message", range },
                            { label: "warning", kind: monaco.languages.CompletionItemKind.Method, insertText: "warning(${1:msg})", insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet, documentation: "Log a warning message", range },
                            { label: "error", kind: monaco.languages.CompletionItemKind.Method, insertText: "error(${1:msg})", insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet, documentation: "Log an error message", range },
                            { label: "debug", kind: monaco.languages.CompletionItemKind.Method, insertText: "debug(${1:msg})", insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet, documentation: "Log a debug message", range },
                        ]
                    };
                }

                if (/self\.data_store\.$/.test(before)) {
                    return {
                        suggestions: [
                            { label: "get", kind: monaco.languages.CompletionItemKind.Method, insertText: "get(${1:key})", insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet, documentation: "Retrieve a value from the data store (async)", range },
                            { label: "set", kind: monaco.languages.CompletionItemKind.Method, insertText: "set(${1:key}, ${2:value})", insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet, documentation: "Store a value in the data store (async)", range },
                        ]
                    };
                }

                if (/self\.message_bus\.$/.test(before)) {
                    return {
                        suggestions: [
                            { label: "send_request", kind: monaco.languages.CompletionItemKind.Method, insertText: "send_request(${1:subject}, ${2:msg})", insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet, documentation: "Send a message and wait for its reply (async)", range },
                            { label: "publish", kind: monaco.languages.CompletionItemKind.Method, insertText: "publish(${1:subject}, ${2:msg})", insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet, documentation: "Notify target of a new message", range },
                        ]
                    };
                }

                if (/self\.system\.$/.test(before)) {
                    return {
                        suggestions: [
                            { label: "call", kind: monaco.languages.CompletionItemKind.Method, insertText: "call(${1:input})", insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet, documentation: "Call the MES system API and wait for its reply (async)", range },
                        ]
                    };
                }

                if (/self\.utils\.$/.test(before)) {
                    return {
                        suggestions: [
                            { label: "sleep", kind: monaco.languages.CompletionItemKind.Method, insertText: "sleep(${1:ms})", insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet, documentation: "Sleep for a number of milliseconds (async)", range },
                            { label: "stringify", kind: monaco.languages.CompletionItemKind.Method, insertText: "stringify(${1:value})", insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet, documentation: "Convert a value to a string representation", range },
                            { label: "convert_value_to_type", kind: monaco.languages.CompletionItemKind.Method, insertText: "convert_value_to_type(${1:value}, ${2:to_type})", insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet, documentation: "Convert a value to a typed value (String, Decimal, Number, Integer, Long, Boolean, Object)", range },
                            { label: "execute_with_retry", kind: monaco.languages.CompletionItemKind.Method, insertText: "execute_with_retry(${1:self.logger}, ${2:attempts}, ${3:sleep_ms}, ${4:code})", insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet, documentation: "Perform an action with retry logic (async)", range },
                            { label: "execute_with_system_error_retry", kind: monaco.languages.CompletionItemKind.Method, insertText: "execute_with_system_error_retry(${1:self.logger}, ${2:attempts}, ${3:sleep_ms}, ${4:code})", insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet, documentation: "Perform an action with retry on MES system errors (async)", range },
                        ]
                    };
                }

                if (/self\.driver\.$/.test(before)) {
                    return {
                        suggestions: [
                            { label: "connect", kind: monaco.languages.CompletionItemKind.Method, insertText: "connect()", insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet, documentation: "Establish connection to the equipment (async)", range },
                            { label: "disconnect", kind: monaco.languages.CompletionItemKind.Method, insertText: "disconnect()", insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet, documentation: "Disconnect the driver (async)", range },
                            { label: "execute_command", kind: monaco.languages.CompletionItemKind.Method, insertText: "execute_command(${1:command}, ${2:parameters})", insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet, documentation: "Execute a command on the device (async)", range },
                            { label: "get_properties", kind: monaco.languages.CompletionItemKind.Method, insertText: "get_properties(${1:properties})", insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet, documentation: "Get property values from the device (async)", range },
                            { label: "set_properties", kind: monaco.languages.CompletionItemKind.Method, insertText: "set_properties(${1:properties_values})", insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet, documentation: "Set properties on the device (async)", range },
                            { label: "send_raw", kind: monaco.languages.CompletionItemKind.Method, insertText: "send_raw(${1:type}, ${2:content})", insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet, documentation: "Send a direct message to the driver and receive a reply (async)", range },
                            { label: "notify_raw", kind: monaco.languages.CompletionItemKind.Method, insertText: "notify_raw(${1:type}, ${2:content})", insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet, documentation: "Send a notification directly to the driver (async)", range },
                            { label: "register_custom_driver_definitions", kind: monaco.languages.CompletionItemKind.Method, insertText: "register_custom_driver_definitions(${1:custom})", insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet, documentation: "Register custom driver definitions (async)", range },
                        ]
                    };
                }

                if (/framework\[['"]$/.test(before)) {
                    return {
                        suggestions: [
                            { label: "logger", kind: monaco.languages.CompletionItemKind.Property, insertText: "logger", documentation: "info / warning / error / debug", range },
                            { label: "data_store", kind: monaco.languages.CompletionItemKind.Property, insertText: "data_store", documentation: "async get / set", range },
                            { label: "message_bus", kind: monaco.languages.CompletionItemKind.Property, insertText: "message_bus", documentation: "async send_request / publish", range },
                            { label: "system", kind: monaco.languages.CompletionItemKind.Property, insertText: "system", documentation: "async call — MES system API", range },
                            { label: "utils", kind: monaco.languages.CompletionItemKind.Property, insertText: "utils", documentation: "sleep / convert_value_to_type / execute_with_retry / execute_with_system_error_retry", range },
                            { label: "lbos", kind: monaco.languages.CompletionItemKind.Property, insertText: "lbos", documentation: "MES business objects — use lbos.Cmf...SomeClass.new()", range },
                            { label: "driver", kind: monaco.languages.CompletionItemKind.Property, insertText: "driver", documentation: "connect / disconnect / execute_command / get_properties / set_properties / send_raw / notify_raw (optional)", range },
                        ]
                    };
                }

                // self.lbos.   /   self.lbos.Cmf.Foundation.BusinessObjects.   etc.
                const lbosMatch = before.match(/\bself\.lbos\.((?:[A-Za-z_$][A-Za-z0-9_$]*\.)*)$/);
                if (lbosMatch) {
                    const pathStr: string = lbosMatch[1] ?? "";
                    const pathSegments = pathStr ? pathStr.replace(/\.$/, "").split(".") : [];

                    let node: unknown = System.LBOS;
                    for (const seg of pathSegments) {
                        if (node == null || (typeof node !== "object" && typeof node !== "function")) {
                            node = null;
                            break;
                        }
                        node = (node as Record<string, unknown>)[seg];
                    }

                    if (node != null) {
                        const suggestions: unknown[] = [];

                        if (typeof node === "function") {
                            suggestions.push({
                                label: "new",
                                kind: monaco.languages.CompletionItemKind.Constructor,
                                insertText: "new()",
                                documentation: "Create a new instance of this class",
                                range
                            });
                        } else if (typeof node === "object") {
                            for (const key of Object.keys(node as object)) {
                                const val = (node as Record<string, unknown>)[key];
                                const kind = typeof val === "function"
                                    ? monaco.languages.CompletionItemKind.Class
                                    : (typeof val === "object" && val !== null)
                                        ? monaco.languages.CompletionItemKind.Module
                                        : monaco.languages.CompletionItemKind.EnumMember;
                                suggestions.push({ label: key, kind, insertText: key, range });
                            }
                        }

                        if (suggestions.length > 0) {
                            return { suggestions };
                        }
                    }
                }

                return { suggestions: [] };
            }
        });
    }

    /**
     * Converts a string with multiple lines to an array of strings.
     * Also replaces tabs with spaces.
     */
    private toMultilineArray(value: string): string[] {
        return value?.replace(/\t/g, "    ").split("\n") || [];
    }

    /** Converts an array of strings to a single string with line breaks */
    private fromMultilineArray(value: string[]): string {
        return value?.join("\n") || "";
    }
}

/** Module */
@NgModule({
    imports: [
        CommonModule,
        CodeEditorModule,
        TaskSettingsModule,
        ValidatorModule,
        BaseWidgetModule,
        PopOverInfoModule,
        PropertyContainerModule,
        PropertyEditorModule,
        ValidatorModule
    ],
    declarations: [PythonCodeSettings]
})
export class PythonCodeSettingsModule { }
