using System.ComponentModel.DataAnnotations;

namespace Common;

public record Share([Range(1, int.MaxValue)] int Id, string Name, double CurrentPrice, int Amount);
