namespace SdkCheck.Tool;

static class JsonReport
{
    public static string Render(IReadOnlyList<Component> components, IReadOnlyList<Finding> findings)
    {
        var report = new Report
        {
            Checked = components.Select(_ => new CheckedComponent
                {
                    Kind = _.Kind.ToString(),
                    Version = _.Version,
                    Label = _.Label
                })
                .ToList(),
            Findings = findings.Select(_ => new ReportedFinding
                {
                    Code = _.Code,
                    Name = Diagnostics.NameFor(_.Code),
                    Kind = _.Component.Kind.ToString(),
                    Version = _.Component.Version,
                    Channel = _.Channel,
                    FixedIn = _.FixedIn,
                    CrossesFeatureBand = _.CrossesFeatureBand,
                    BandNewest = _.BandNewest,
                    EolDate = _.EolDate,
                    Message = _.Body(),
                    Cves = _.Cves.Select(cve => new ReportedCve { Id = cve.Id, Url = cve.Url }).ToList()
                })
                .ToList()
        };

        return JsonSerializer.Serialize(report, ReportJsonContext.Default.Report);
    }
}
