using System.ComponentModel.DataAnnotations;

namespace TestProject1;

public class User
{
    public string Id { get; set; } = null!;

    public string? Email { get; set; }

    [Required]
    public DateTime CreatedDate { get; set; }

    public virtual ICollection<Hobby>? Hobbies { get; set; }

}


public class Hobby
{
    public string Id { get; set; } = null!;

    public string? Name { get; set; }

    public virtual User User { get; set; } = null!;

}

