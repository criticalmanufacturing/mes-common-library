import { Injectable } from "@angular/core";

export interface RoslynDiagnostic {
    severity: "error" | "warning" | "info";
    message: string;
    line?: number;
    column?: number;
}

export interface RoslynCompilationResult {
    assemblyBase64: string;
    sourceHash: string;
    catalogVersion: string;
    diagnostics: RoslynDiagnostic[];
}

/** Mirrors CompletionItemResult from CompilerInterop.cs — kind is Roslyn's own tag name (e.g.
 * "Method", "ExtensionMethod", "Property"), mapped to a Monaco CompletionItemKind by the caller. */
export interface RoslynCompletionItem {
    label: string;
    kind: string;
    sortText: string;
}

interface BrowserRoslynCompiler {
    compile(source: string): Promise<RoslynCompilationResult>;
    getCompletions(source: string, position: number): Promise<RoslynCompletionItem[]>;
    getCompletionDescription(source: string, position: number, label: string, sortText: string): Promise<string | null>;
}

declare global {
    interface Window { __roslynCodeCompiler?: BrowserRoslynCompiler; }
}

/** Adapter for the lazily-loaded Roslyn WASM compiler supplied by package assets. */
@Injectable({ providedIn: "root" })
export class RoslynCompilerBrowser {
    public async compile(source: string): Promise<RoslynCompilationResult> {
        const compiler = await this._ensureLoaded();
        return compiler.compile(source);
    }

    /** @param position 0-based character offset into `source` (e.g. Monaco's model.getOffsetAt(...)), not a line/column pair. */
    public async getCompletions(source: string, position: number): Promise<RoslynCompletionItem[]> {
        const compiler = await this._ensureLoaded();
        return compiler.getCompletions(source, position);
    }

    /** Lazily resolves the documentation for one previously-returned completion item (see getCompletions). */
    public async getCompletionDescription(source: string, position: number, label: string, sortText: string): Promise<string | null> {
        const compiler = await this._ensureLoaded();
        return compiler.getCompletionDescription(source, position, label, sortText);
    }

    private async _ensureLoaded(): Promise<BrowserRoslynCompiler> {
        if (window.__roslynCodeCompiler == null) {
            const module = await import(/* @vite-ignore */ new URL("roslyn-compiler/compiler.js", document.baseURI).href) as { initialize(): Promise<void> };
            await module.initialize();
        }
        if (window.__roslynCodeCompiler == null) {
            throw new Error("The Roslyn browser compiler could not be initialized.");
        }
        return window.__roslynCodeCompiler;
    }
}