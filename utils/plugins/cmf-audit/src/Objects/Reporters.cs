using audit.Services;
using Spectre.Console;
using System.IO.Abstractions;

namespace audit.Objects
{
    public class Reporters(
        List<Action<IDirectoryInfo, string[]>> panelReports = null,
        List<Action<IDirectoryInfo>> tableReports = null,
        List<PostTelemetry> postEvents = null,
        BarChart chart = null)
    {
        public BarChart chart = chart ?? new();
        public List<Action<IDirectoryInfo, string[]>> PanelReports = panelReports ?? [];
        public List<Action<IDirectoryInfo>> TableReports = tableReports ?? [];
        public List<PostTelemetry> PostEvents = postEvents ?? [];

        public Action<string, string, Dictionary<string, string>> RowAdder;
        public Action<string, string, List<Tag>, Dictionary<string, string>> RowAdderWithTags;
    }
}
