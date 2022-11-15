namespace OrderedActionBlock.Interfaces
{
    public interface IProcessingStateManager<TInput> where TInput : IHasUniqueId
    {
        void Enqueue(TInput input);
        LinkedListNode<TInput>? Dequeue();
        void AddToProcessing(TInput input);
        void AddToBlocked(TInput input);
        bool InBlocked(TInput input);
        bool InProcessing(TInput input);
        int QueuedUniqueCount();
        int QueuedCount();
        void RemoveFromQueue(LinkedListNode<TInput> linkedListNode);
        bool RemoveFromProcessing(TInput input);
        void ClearBlocked();
    }
}