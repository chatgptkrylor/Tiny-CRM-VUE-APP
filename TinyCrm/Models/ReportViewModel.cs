using System.Collections.Generic;

namespace TinyCrm.Models
{
    public class StatusSummaryRow
    {
        public CustomerStatus Status { get; set; }
        public int Count { get; set; }
    }

    public class InteractionTypeRow
    {
        public InteractionType Type { get; set; }
        public int Count { get; set; }
    }

    public class CustomerReportRow
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Company { get; set; }
        public CustomerStatus Status { get; set; }
        public int InteractionCount { get; set; }
        public System.DateTime? LastInteractionDate { get; set; }
    }

    public class ReportViewModel
    {
        public IList<StatusSummaryRow> StatusSummary { get; set; } = new List<StatusSummaryRow>();
        public IList<InteractionTypeRow> InteractionTypeSummary { get; set; } = new List<InteractionTypeRow>();
        public IList<CustomerReportRow> Customers { get; set; } = new List<CustomerReportRow>();
    }
}