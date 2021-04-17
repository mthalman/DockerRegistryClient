using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Rest;

namespace Valleysoft.DockerRegistryClient
{
    public static class BlobOperationsExtensions
    {
        public static async Task<Stream> GetAsync(this IBlobOperations operations, string repositoryName, string digest, CancellationToken cancellationToken = default)
        {
            HttpOperationResponse<Stream> response =
                await operations.GetWithHttpMessagesAsync(repositoryName, digest, cancellationToken).ConfigureAwait(false);
            return new BlobStream(response);
        }

        private class BlobStream : Stream
        {
            private readonly HttpOperationResponse<Stream> response;

            internal BlobStream(HttpOperationResponse<Stream> response)
            {
                this.response = response;
            }

            public override bool CanRead => this.response.Body.CanRead;

            public override bool CanSeek => this.response.Body.CanSeek;

            public override bool CanWrite => this.response.Body.CanWrite;

            public override bool CanTimeout => this.response.Body.CanTimeout;

            public override int ReadTimeout
            {
                get => this.response.Body.ReadTimeout;
                set => this.response.Body.ReadTimeout = value;
            }

            public override int WriteTimeout
            {
                get => this.response.Body.WriteTimeout;
                set => this.response.Body.WriteTimeout = value;
            }

            public override long Length => this.response.Body.Length;

            public override long Position
            {
                get => this.response.Body.Position;
                set => this.response.Body.Position = value;
            }

            public override void Flush() => this.response.Body.Flush();

            public override int Read(byte[] buffer, int offset, int count) => this.response.Body.Read(buffer, offset, count);

            public override long Seek(long offset, SeekOrigin origin) => this.response.Body.Seek(offset, origin);

            public override void SetLength(long value) => this.response.Body.SetLength(value);

            public override void Write(byte[] buffer, int offset, int count) => this.response.Body.Write(buffer, offset, count);

            public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state) =>
                this.response.Body.BeginRead(buffer, offset, count, callback, state);

            public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state) =>
                this.response.Body.BeginWrite(buffer, offset, count, callback, state);

            public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken) =>
                this.response.Body.CopyToAsync(destination, bufferSize, cancellationToken);

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    this.response.Dispose();
                }

                base.Dispose(disposing);
            }

            public override int EndRead(IAsyncResult asyncResult) =>
                this.response.Body.EndRead(asyncResult);

            public override void EndWrite(IAsyncResult asyncResult) =>
                this.response.Body.EndWrite(asyncResult);

            public override Task FlushAsync(CancellationToken cancellationToken) =>
                this.response.Body.FlushAsync(cancellationToken);

            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
                this.response.Body.ReadAsync(buffer, offset, count, cancellationToken);

            public override int ReadByte() =>
                this.response.Body.ReadByte();

            public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
                this.response.Body.WriteAsync(buffer, offset, count, cancellationToken);

            public override void WriteByte(byte value) =>
                this.response.Body.WriteByte(value);
        }
    }
}
