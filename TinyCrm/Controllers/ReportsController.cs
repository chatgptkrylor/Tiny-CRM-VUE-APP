using System;
using System.Linq;
using System.Text;
using System.Web.Mvc;
using TinyCrm.Data.Repositories;
using TinyCrm.Models;

namespace TinyCrm.Controllers
{
    public class ReportsController : Controller
    {
        private readonly CustomerRepository _customers = new CustomerRepository();
        private readonly InteractionRepository _interactions = new InteractionRepository();

        public ActionResult Index()
        {
            var customers = _customers.GetAll();
            var interactions = _interactions.GetAll();

            var model = new ReportViewModel();

            foreach (CustomerStatus status in Enum.GetValues(typeof(CustomerStatus)))
            {
                model.StatusSummary.Add(new StatusSummaryRow
                {
                    Status = status,
                    Count = customers.Count(c => c.Status == status)
                });
            }

            foreach (InteractionType type in Enum.GetValues(typeof(InteractionType)))
            {
                model.InteractionTypeSummary.Add(new InteractionTypeRow
                {
                    Type = type,
                    Count = interactions.Count(i => i.Type == type)
                });
            }

            foreach (var c in customers.OrderBy(c => c.Name))
            {
                model.Customers.Add(new CustomerReportRow
                {
                    Id = c.Id,
                    Name = c.Name,
                    Company = c.Company,
                    Status = c.Status,
                    InteractionCount = interactions.Count(i => i.CustomerId == c.Id),
                    LastInteractionDate = c.LastInteractionDate
                });
            }

            return View(model);
        }

        [HttpGet]
        public ActionResult ExportCsv()
        {
            var customers = _customers.GetAll();
            var interactions = _interactions.GetAll();

            var sb = new StringBuilder();
            sb.AppendLine("Id,Name,Company,Email,Phone,Status,InteractionCount,LastInteraction");

            foreach (var c in customers.OrderBy(c => c.Name))
            {
                var interactionCount = interactions.Count(i => i.CustomerId == c.Id);
                var lastInteraction = c.LastInteractionDate.HasValue
                    ? c.LastInteractionDate.Value.ToString("yyyy-MM-dd")
                    : "";

                sb.AppendLine(string.Join(",",
                    c.Id,
                    CsvEscape(c.Name),
                    CsvEscape(c.Company),
                    CsvEscape(c.Email),
                    CsvEscape(c.Phone),
                    c.Status.ToString(),
                    interactionCount,
                    lastInteraction));
            }

            Response.AddHeader("Content-Disposition", "attachment;filename=customers.csv");
            return Content(sb.ToString(), "text/csv");
        }

        private static string CsvEscape(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            if (value.Contains(",") || value.Contains("\"") || value.Contains(Environment.NewLine))
            {
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            }
            return value;
        }
    }
}