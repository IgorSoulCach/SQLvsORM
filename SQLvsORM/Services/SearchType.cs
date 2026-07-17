namespace SQLvsORM.Services;

public enum SearchType
{
    Equals = 1,
    NotEquals = 2,
    GreaterThan = 3,
    LessThan = 4,
    GreaterOrEqual = 5,
    LessOrEqual = 6,
    Between = 7,
    Contains = 8,
    In = 9,
    NotIn =10,
    Before = 11,
    After = 12
}
