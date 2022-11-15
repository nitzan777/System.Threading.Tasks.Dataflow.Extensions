namespace OrderedActionBlock.Interfaces
{
    public interface IOrderedActionBlock<in TInput> where TInput : IHasUniqueId
    {
        Task Completion { get; }
        Task Complete();
        Task<bool> SendAsync(TInput input);
        int InputCount { get; }
        Func<Exception, Task>? OnError { get; set; }
    }
}