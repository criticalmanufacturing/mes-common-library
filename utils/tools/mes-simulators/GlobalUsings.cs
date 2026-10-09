// Cmf.Navigo.BusinessObjects declares its own "Task" business object (a work order task), which
// collides with System.Threading.Tasks.Task wherever both namespaces are in scope. Alias it globally
// so every file gets the async Task without needing a per-file using.
global using Task = System.Threading.Tasks.Task;
