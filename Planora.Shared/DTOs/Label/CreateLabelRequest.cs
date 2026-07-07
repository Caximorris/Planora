namespace Planora.Shared.DTOs.Label;

public class CreateLabelRequest
{
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = Planora.Shared.Constants.PlanoraColors.DefaultLabelColor;
}
