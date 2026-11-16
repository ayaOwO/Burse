namespace Common.Exceptions;

public sealed class InsufficientFundsException() : Exception("Insufficient funds");

public sealed class InsufficientStocksException() : Exception("Insufficient stocks");
