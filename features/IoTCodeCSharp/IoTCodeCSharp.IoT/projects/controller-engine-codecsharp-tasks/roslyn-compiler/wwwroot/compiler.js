// Bootstraps the headless Blazor WASM host (no rendered UI — this app only exists to run
// CompilerInterop.Compile in-browser) and exposes it as window.__roslynCodeCompiler, which
// roslynCompilerBrowser.ts dynamically imports this module to set up. There's no wwwroot/index.html
// here for blazor.webassembly.js to auto-discover its own <script> tag, so it's injected manually
// with autostart disabled and started explicitly once loaded.

let readyPromise;

function loadScript(url) {
    return new Promise((resolve, reject) => {
        const script = document.createElement("script");
        script.src = url;
        script.setAttribute("autostart", "false");
        script.onload = () => resolve();
        script.onerror = () => reject(new Error(`Failed to load ${url}`));
        document.head.appendChild(script);
    });
}

// This module's own base ("…/roslyn-compiler/"), independent of the host page's <base href>/
// document.baseURI. blazor.webassembly.js resolves every boot resource (dotnet.js, the wasm
// runtime, assemblies, ...) as `new URL(defaultUri, document.baseURI)`, which is wrong here since
// this app is nested under a host page rooted elsewhere — loadBootResource below overrides that by
// handing back an already-absolute URL anchored at this module instead.
const appBaseUrl = new URL(".", import.meta.url);

async function start() {
    await loadScript(new URL("_framework/blazor.webassembly.js", appBaseUrl).href);
    await window.Blazor.start({
        loadBootResource: (type, name, defaultUri, integrity) => new URL(defaultUri, appBaseUrl).href
    });
    window.__roslynCodeCompiler = {
        compile: source => window.DotNet.invokeMethodAsync("RoslynCode.Compiler", "Compile", source),
        getCompletions: (source, position) => window.DotNet.invokeMethodAsync("RoslynCode.Compiler", "GetCompletions", source, position),
        getCompletionDescription: (source, position, label, sortText) =>
            window.DotNet.invokeMethodAsync("RoslynCode.Compiler", "GetCompletionDescription", source, position, label, sortText)
    };
}

export function initialize() {
    readyPromise ??= start();
    return readyPromise;
}
