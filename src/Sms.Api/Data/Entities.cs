namespace Sms.Api.Data;

public class Employee
{
    public int Id { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Address { get; set; } = "";
    public List<EmployeeInstance> Instances { get; set; } = new();
}

/// <summary>One (employee, phone) pairing. The same phone may belong to several employees.</summary>
public class EmployeeInstance
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public string PhoneNumber { get; set; } = "";
}

public enum MessageStatus { Pending, Sending, Sent, Delivered, Failed }

public class Message
{
    public int Id { get; set; }
    public string To { get; set; } = "";
    public string Body { get; set; } = "";
    public MessageStatus Status { get; set; } = MessageStatus.Pending;
    public string? IdempotencyKey { get; set; }
    public string? ProviderName { get; set; }
    public string? ProviderMessageId { get; set; }
    public string? Error { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SentAt { get; set; }
}
