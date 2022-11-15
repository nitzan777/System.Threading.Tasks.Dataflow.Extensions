using System.Collections.Concurrent;
using OrderedActionBlock.Interfaces;

namespace System.Threading.Tasks.Dataflow.Extensions.OrderedActionBlock
{
    internal class ProcessingStateManager<TInput> : IProcessingStateManager<TInput> where TInput : IHasUniqueId
    {
        private readonly LinkedList<TInput> _waitingList;
        private readonly IDictionary<string, int> _waitingItems;
        private readonly IDictionary<string, int> _itemsInProcess;
        private readonly ISet<string> _blocked;

        public ProcessingStateManager()
        {
            _waitingItems = new ConcurrentDictionary<string, int>();
            _waitingList = new LinkedList<TInput>();
            _itemsInProcess = new ConcurrentDictionary<string, int>();
            _blocked = new HashSet<string>();
        }

        public void AddToProcessing(TInput input)
        {
            lock (_itemsInProcess)
            {
                if (!_itemsInProcess.ContainsKey(input.Id))
                {
                    _itemsInProcess.Add(input.Id, 0);
                }
            }
        }
        public void AddToBlocked(TInput input)
        {
            lock (_blocked)
            {
                _blocked.Add(input.Id);
            }
        }
        public void Enqueue(TInput input)
        {
            lock (_waitingItems)
            {
                try
                {
                    _waitingList.AddLast(input);

                    if (!_waitingItems.ContainsKey(input.Id))
                    {
                        _waitingItems[input.Id] = 1;
                    }
                    else
                    {
                        _waitingItems[input.Id] += 1;
                    }
                }
                catch (Exception e)
                {
                    throw new Exception($"error while adding input: '{input.Id}'", e);
                }
            }
        }
        public void RemoveFromQueue(LinkedListNode<TInput> linkedListNode)
        {
            lock (_waitingItems)
            {
                try
                {
                    _waitingList.Remove(linkedListNode);

                    var count = _waitingItems[linkedListNode.Value.Id];

                    if (count == 1)
                    {
                        _waitingItems.Remove(linkedListNode.Value.Id);
                    }
                    else
                    {
                        _waitingItems[linkedListNode.Value.Id] = count - 1;
                    }
                }
                catch (Exception e)
                {
                    throw new Exception($"error while removing input: '{linkedListNode.Value.Id}'", e);
                }
            }
        }
        public bool RemoveFromProcessing(TInput input)
        {
            lock (_itemsInProcess)
            {
                return _itemsInProcess.Remove(input.Id);
            }
        }
        public void ClearBlocked()
        {
            lock (_blocked)
            {
                _blocked.Clear();
            }
        }
        public bool InBlocked(TInput input)
        {
            lock (_blocked)
            {
                return _blocked.Contains(input.Id);
            }
        }
        public bool InProcessing(TInput input)
        {
            lock (_itemsInProcess)
            {
                return _itemsInProcess.ContainsKey(input.Id);
            }
        }
        public int QueuedUniqueCount()
        {
            lock (_waitingItems)
            {
                return _waitingItems.Count;
            }
        }
        public int QueuedCount()
        {
            lock (_waitingItems)
            {
                return _waitingItems.Values.Sum();
            }
        }
        public LinkedListNode<TInput>? Dequeue()
        {
            lock (_waitingItems)
            {
                return _waitingList.First;
            }
        }
    }
}
