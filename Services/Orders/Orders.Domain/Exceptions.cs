namespace Orders.Domain;

public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
}

public class BusinessValidationException : Exception
{
    public BusinessValidationException(string message) : base(message) { }
}

public class BusinessConflictException : Exception
{
    public BusinessConflictException(string message) : base(message) { }
}