// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Threading;

namespace System.IO.Pipes
{
    internal sealed class PipeIOCompletionSource : PipeCompletionSource<int>
    {
        private readonly bool _isWrite;
        private readonly PipeStream _pipeStream;

        private bool _isMessageComplete;
        private int _numBytes; // number of buffer read OR written

        internal PipeIOCompletionSource(PipeStream stream, byte[] buffer, CancellationToken cancellationToken, bool isWrite)
            : base(stream._threadPoolBinding, cancellationToken, pinData: buffer)
        {
            Debug.Assert(buffer != null, "buffer is null");

            _pipeStream = stream;
            _isWrite = isWrite;
            _isMessageComplete = true;
            _numBytes = 0;
        }

        internal override void SetCompletedSynchronously()
        {
            if (!_isWrite)
            {
                _pipeStream.UpdateMessageCompletion(_isMessageComplete);
            }

            TrySetResult(_numBytes);
        }

        protected override void AsyncCallback(uint errorCode, uint numBytes)
        {
            _numBytes = (int)numBytes;

            // Allow async read to finish
            if (!_isWrite)
            {
                switch (errorCode)
                {
                    case Interop.mincore.Errors.ERROR_BROKEN_PIPE:
                    case Interop.mincore.Errors.ERROR_PIPE_NOT_CONNECTED:
                    case Interop.mincore.Errors.ERROR_NO_DATA:
                        errorCode = 0;
                        break;
                }
            }

            // For message type buffer.
            if (errorCode == Interop.mincore.Errors.ERROR_MORE_DATA)
            {
                errorCode = 0;
                _isMessageComplete = false;
            }
            else
            {
                _isMessageComplete = true;
            }

            base.AsyncCallback(errorCode, numBytes);
        }

        protected override void HandleError()
        {
            TrySetException(_pipeStream.WinIOError(ErrorCode));
        }
    }
}
