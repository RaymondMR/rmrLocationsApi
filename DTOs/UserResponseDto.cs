namespace WebAppTest1.DTOs
{
    public class UserResponseDto
    {
        public Guid Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public List<string> Roles { get; set; } = [];
    }
}
