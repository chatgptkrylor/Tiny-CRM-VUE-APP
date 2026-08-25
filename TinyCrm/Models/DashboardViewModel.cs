using System;
using System.Collections.Generic;

namespace TinyCrm.Models
{
    public class DashboardViewModel
    {
        public int TotalCustomers { get; set; }
        public int TotalInteractions { get; set; }
        public IDictionary<CustomerStatus, int> CustomersByStatus { get; set; } = new Dictionary<CustomerStatus, int>();
        public IDictionary<InteractionType, int> InteractionsByType { get; set; } = new Dictionary<InteractionType, int>();
        public IList<Interaction> RecentInteractions { get; set; } = new List<Interaction>();
        public int NeedsFollowUps { get; set; }
    }
}