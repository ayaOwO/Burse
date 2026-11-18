using Common.Models;

namespace BL.Abstractions;

public interface IOrderMatcher
{
    public List<Trade> MatchOrder(Order newOrder);
}
