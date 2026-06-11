namespace AsyncApi.Exceptions;

public abstract class AppException(string message) : Exception(message);

public sealed class NotFoundException(string message) : AppException(message);

public sealed class AppValidationException(string message) : AppException(message);
