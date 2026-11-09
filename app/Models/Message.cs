using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Unbreakable.Models;

// Elke codeboeklijn mag maar één keer gebruikt worden tussen dezelfde afzender en ontvanger.
[Index(nameof(Sender), nameof(Recipient), nameof(KeyLine), IsUnique = true)]
public class Message
{
    public int Id { get; set; }

    [Required]
    [StringLength(40, MinimumLength = 2)]
    public string Sender { get; set; }

    [Required]
    [StringLength(40, MinimumLength = 2)]
    public string Recipient { get; set; }

    // Versleuteld onderwerp: gebruikt het begin van de codeboeklijn.
    [Required]
    [MaxLength(40)]
    [RegularExpression("^[A-Z0-9_]{1,40}$")]
    public string Subject { get; set; }

    // Versleuteld bericht: gebruikt de rest van dezelfde lijn (onderwerp + bericht samen max. 40).
    [Required]
    [MaxLength(40)]
    [RegularExpression("^[A-Z0-9_]{1,40}$")]
    public string Body { get; set; }

    // Nummer van de gebruikte lijn in het codeboek.
    [Range(1, 50)]
    public int KeyLine { get; set; }

    // HMAC-SHA256 in hex (32 bytes).
    [Required]
    [MaxLength(64)]
    [RegularExpression("^[0-9a-f]{64}$")]
    public string Mac { get; set; }

    public DateTime SentAt { get; set; }
}

// Wat de client mag meesturen. Id, Sender en SentAt bepaalt de server zelf.
public record NewMessage(
    [property: Required, StringLength(40, MinimumLength = 2)] string Recipient,
    [property: Required, RegularExpression("^[A-Z0-9_]{1,40}$")] string Subject,
    [property: Required, RegularExpression("^[A-Z0-9_]{1,40}$")] string Body,
    [property: Range(1, 50)] int KeyLine,
    [property: Required, RegularExpression("^[0-9a-f]{64}$")] string Mac);
