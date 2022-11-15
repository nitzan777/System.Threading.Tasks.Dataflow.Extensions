using System.Threading.Tasks.Dataflow;
using Nitzan777.System.Threading.Tasks.Dataflow.Extensions.OrderedActionBlock.Interfaces;

namespace Nitzan777.System.Threading.Tasks.Dataflow.Extensions.OrderedActionBlock
{
    public class OrderedActionBlock<TInput> : IOrderedActionBlock<TInput> where TInput : IHasUniqueId
    {
        #region Private Members
        private readonly IProcessingStateManager<TInput>? _processingStateManager;
        private readonly ActionBlock<TInput> _block;
        private readonly Func<TInput, Task<bool>> _sendAsync;
        private readonly Func<Task> _complete;
        private readonly Func<int> _inputCount;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private const int ProcessWaitingInterval = 100;
        private bool _completed;
        #endregion

        #region Public Propeties
        public Task Completion => _block.Completion;
        public int InputCount => _inputCount();
        public Func<Exception, Task>? OnError { get; set; }
        #endregion

        #region Ctors
        public OrderedActionBlock(Func<TInput, Task> func, ExecutionDataflowBlockOptions? options = default)
        {
            var opt = options ?? new ExecutionDataflowBlockOptions();
            _cancellationTokenSource = new CancellationTokenSource();

            if (opt.MaxDegreeOfParallelism == 1)
            {
                _block = new ActionBlock<TInput>(Func(func), opt);
                _sendAsync = async item => await _block.SendAsync(item, _cancellationTokenSource.Token).ConfigureAwait(false);
                _inputCount = () => _block.InputCount;
                _complete = () =>
                {
                    _completed = true;
                    _block.Complete();
                    return Task.CompletedTask;
                };
                return;
            }

            _block = new ActionBlock<TInput>(KeepOrderFunc(func), opt);
            _processingStateManager = new ProcessingStateManager<TInput>();

            _sendAsync = async item =>
            {
                try
                {
                    if (_completed)
                    {
                        return false;
                    }

                    _processingStateManager.Enqueue(item);
                    return true;
                }
                catch (Exception e)
                {
                    await OnError?.Invoke(new Exception($"error while sending input: '{item.Id}'", e))!;
                    return false;
                }
            };

            _inputCount = () => _block.InputCount + _processingStateManager.QueuedCount();

            _complete = async () =>
            {
                _completed = true;
                await Task.Run(async () =>
                {
                    while (_processingStateManager.QueuedCount() != 0)
                    {
                        await Task.Delay(100);
                    }
                });
                _block.Complete();
            };

            Task.Run(ProcessQueued);
        }
        #endregion

        #region Private Methods
        private Func<TInput, Task> KeepOrderFunc(Func<TInput, Task> func)
        {
            return async item =>
            {
                try
                {
                    await func(item).ConfigureAwait(false);
                    _processingStateManager?.RemoveFromProcessing(item);
                }
                catch (Exception e)
                {
                    await OnError?.Invoke(new Exception($"error while processing input: '{item.Id}'", e))!;
                }
            };
        }
        private Func<TInput, Task> Func(Func<TInput, Task> func)
        {
            return async item =>
            {
                try
                {
                    await func(item).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    await OnError?.Invoke(new Exception($"error while processing input: '{item.Id}'", e))!;
                }
            };
        }
        private async Task<bool> SendToProcess(TInput input)
        {
            _processingStateManager?.AddToProcessing(input);

            var result = await _block.SendAsync(input, _cancellationTokenSource.Token).ConfigureAwait(false);
            if (!result)
            {
                _processingStateManager?.RemoveFromProcessing(input);
            }

            return result;
        }
        private async Task ProcessQueued()
        {
            while (!_cancellationTokenSource.IsCancellationRequested)
            {
                try
                {
                    if (_processingStateManager!.QueuedUniqueCount() < 1)
                    {
                        await Task.Delay(ProcessWaitingInterval, _cancellationTokenSource.Token).ConfigureAwait(false);
                        continue;
                    }

                    _processingStateManager.ClearBlocked();

                    var item = _processingStateManager.Dequeue();

                    while (item != null)
                    {
                        try
                        {
                            if (_processingStateManager.InBlocked(item.Value))
                            {
                                item = item.Next;
                                continue;
                            }

                            if (_processingStateManager.InProcessing(item.Value))
                            {
                                _processingStateManager.AddToBlocked(item.Value);
                                item = item.Next;
                                continue;
                            }

                            var result = await SendToProcess(item.Value).ConfigureAwait(false);

                            if (!result)
                            {
                                _processingStateManager.AddToBlocked(item.Value);
                                continue;
                            }

                            _processingStateManager.RemoveFromQueue(item);
                            item = item.Next;
                        }
                        catch (Exception e)
                        {
                            await OnError?.Invoke(new Exception($"error while handling waiting input: '{item?.Value.Id}'",
                                e))!;
                        }
                    }
                }
                catch (Exception e)
                {
                    await OnError?.Invoke(new Exception("error while ProcessQueued function", e))!;
                }
            }
        }
        #endregion

        #region Public Methods
        public Task Complete()
        {
            return _complete();
        }
        public Task<bool> SendAsync(TInput input)
        {
            return _sendAsync(input);
        }
        #endregion
    }
}