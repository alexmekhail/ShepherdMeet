namespace MinimalApiProject.Models
{
    public class Appointment
    {
        public int ID { get; set; }
        public int UserID { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty;
        public string Time { get; set; } = string.Empty;
        public int? AvailabilityID { get; set; }
        public DateTime? StartUtc { get; set; }
        public DateTime? EndUtc { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
