namespace MESSimulator.Pipeline
{
    /// <summary>
    /// Asks the user before the simulator terminates previous runs (Line:Startup:ConfirmTerminate).
    /// </summary>
    public interface ITerminationConfirmation
    {
        /// <summary>Whether the user agrees to <paramref name="question"/>; throws when there is nobody to ask.</summary>
        bool Confirm(string question);
    }

    /// <summary>
    /// Asks on the console. Without one (input redirected, e.g. a container) it refuses: the termination then needs --yes
    /// (or Line:Startup:ConfirmTerminate=false).
    /// </summary>
    public sealed class ConsoleTerminationConfirmation : ITerminationConfirmation
    {
        public bool Confirm(string question)
        {
            if (Console.IsInputRedirected)
            {
                throw new InvalidOperationException(
                    $"{question} There is no console to confirm it: run with --yes, or set Line:Startup:ConfirmTerminate to false.");
            }

            Console.Write($"{question} [y/N] ");
            var answer = Console.ReadLine()?.Trim();
            return string.Equals(answer, "y", StringComparison.OrdinalIgnoreCase)
                || string.Equals(answer, "yes", StringComparison.OrdinalIgnoreCase);
        }
    }
}
