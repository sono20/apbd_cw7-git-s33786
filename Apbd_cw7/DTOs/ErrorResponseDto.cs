namespace Apbd_cw7.DTOs;

public class ErrorResponseDto
{
    public string Message { get; set; } = string.Empty;
    public DateTime TimeStamp { get; set; }

    public ErrorResponseDto(string message)
    {
        Message = message;
        TimeStamp = DateTime.Now;
    }
}