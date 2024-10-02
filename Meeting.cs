namespace MinimalApiProject.Models
{
public class Meeting {
    public int ID { get; set; }
    public int UsersID { get; set; }
    public string Title { get; set; }
    public DateTime Date { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public int LocationID { get; set; }
}
}
