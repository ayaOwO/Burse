using System.ComponentModel.DataAnnotations;

namespace Common.Models;

public record Share([Range(1, int.MaxValue)] int Id, string Name, double CurrentPrice, int Amount);
