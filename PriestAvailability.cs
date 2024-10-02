namespace MinimalApiProject.Models
{
    public class PriestAvailabilityInput
    {
        public int ID { get; set; }
        public int UserID { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public List<string> Days { get; set; } = new List<string>(); // e.g., ["Monday", "Wednesday", "Friday"]
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public bool IsAvailable { get; set; } = true;
    }
}
