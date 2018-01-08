using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Remoting;
using System.Text;

namespace Mercurial
{
    /// <summary>
    /// This class implements <see cref="IClient"/> by spinning up an instance when
    /// first instantiated, and keeping the instance around and communicating with it
    /// over standard input and output, using the new "command server mode" introduced
    /// in Mercurial 1.9.
    /// </summary>
    public sealed class PersistentClient : IClient, IDisposable
    {
        static readonly int MercurialHeaderLength = 5;
        /// <summary>
        /// This is the backing field for the <see cref="RepositoryPath"/> property.
        /// </summary>
        private readonly string _RepositoryPath;

        /// <summary>
        /// This field holds a link to the persistent client running alongside Mercurial.Net.
        /// </summary>
        private Process _Process;

        /// <summary>
        /// Initializes a new instance of the <see cref="PersistentClient"/> class.
        /// </summary>
        /// <param name="repositoryPath">
        /// The path to the repository this <see cref="PersistentClient"/> will handle.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// <para><paramref name="repositoryPath"/> is <c>null</c> or empty.</para>
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// The <see cref="PersistentClient"/> is not supported for the specified repository path,
        /// or the current Mercurial client.
        /// </exception>
        public PersistentClient(string repositoryPath)
        {
            if (StringEx.IsNullOrWhiteSpace(repositoryPath))
                throw new ArgumentNullException("repositoryPath");

            if (!IsSupported(repositoryPath))
                throw new NotSupportedException("The persistent client is not supported for the given repository or by the current Mercurial client");

            _RepositoryPath = repositoryPath;
            ClientExecutable.Configuration.Refresh(repositoryPath);
            StartPersistentMercurialClient();
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="PersistentClient"/> class.
        /// </summary>
        ~PersistentClient()
        {
            Dispose();
        }

        /// <summary>
        /// Gets the path to the repository this <see cref="PersistentClient"/> is handling.
        /// </summary>
        public string RepositoryPath
        {
            get
            {
                return _RepositoryPath;
            }
        }

        /// <summary>
        /// Executes the given <see cref="IMercurialCommand"/> command against
        /// the Mercurial repository.
        /// </summary>
        /// <param name="command">
        /// The <see cref="IMercurialCommand"/> command to execute.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// <para><paramref name="command"/> is <c>null</c>.</para>
        /// </exception>
        /// <exception cref="MercurialException">
        /// HG did not complete within the allotted time.
        /// </exception>
        public void Execute(IMercurialCommand command)
        {
            if (command == null)
                throw new ArgumentNullException("command");

            if (_Process == null)
                StartPersistentMercurialClient();

            command.Validate();
            command.Before();

            IEnumerable<string> arguments = new[]
            {
                command.Command,
                 "--noninteractive",
            };
            arguments = arguments.Concat(command.Arguments.Where(a => !StringEx.IsNullOrWhiteSpace(a)));
            arguments = arguments.Concat(command.AdditionalArguments.Where(a => !StringEx.IsNullOrWhiteSpace(a)));

            var commandParts = arguments.ToArray();

            string commandEncoded = string.Join("\0", commandParts.Select(p => p.Trim('"')).ToArray());
            int length = commandEncoded.Length;
            var commandBuffer = new StringBuilder();
            commandBuffer.Append("runcommand\n");
            commandBuffer.Append((char)((length >> 24) & 0xff));
            commandBuffer.Append((char)((length >> 16) & 0xff));
            commandBuffer.Append((char)((length >> 8) & 0xff));
            commandBuffer.Append((char)(length & 0xff));
            commandBuffer.Append(commandEncoded);

            string commandArguments = null;
            if (command.Observer != null)
            {
                commandArguments = string.Join(" ", commandParts.Skip(1).ToArray());
                command.Observer.Executing(command.Command, commandArguments);
            }

            MemoryStream output = new MemoryStream();
            MemoryStream error = new MemoryStream();
            var outputs = new Dictionary<CommandChannel, Stream>() {
                { CommandChannel.Output, output },
                { CommandChannel.Error, error },
            };

            var _codec = ClientExecutable.GetMainEncoding();

            int resultCode = RunCommand(commandParts, outputs, null);
            var result = new CommandResult(_codec.GetString(output.GetBuffer(), 0, (int)output.Length),
                                      _codec.GetString(error.GetBuffer(), 0, (int)error.Length),
                                      resultCode);
            
            if (resultCode == 0 || !string.IsNullOrEmpty(result.Output))
            {
                if (command.Observer != null)
                {
                    command.Observer.Output(result.Output);
                    command.Observer.ErrorOutput(result.Error);
                    command.Observer.Executed(command.Command, commandArguments, resultCode, result.Output, result.Error);
                }
                command.After(resultCode, result.Output, result.Error);
                return;
            }

            StopPersistentMercurialClient();
            throw new MercurialExecutionException(
                string.IsNullOrEmpty(result.Error) ?
                "Unable to decode output from executing command, spinning down persistent client"
                : result.Error);
        }

        internal static int ReadInt(byte[] buffer, int offset)
        {
            if (null == buffer) throw new ArgumentNullException("buffer");
            if (buffer.Length < offset + 4) throw new ArgumentOutOfRangeException("offset");

            return IPAddress.NetworkToHostOrder(BitConverter.ToInt32(buffer, offset));
        }

        private int RunCommand(IList<string> command,
                               IDictionary<CommandChannel, Stream> outputs,
                               IDictionary<CommandChannel, Func<uint, byte[]>> inputs)
        {
            if (null == command || 0 == command.Count)
                throw new ArgumentException("Command must not be empty", "command");

            var _codec = ClientExecutable.GetMainEncoding();

            byte[] commandBuffer = _codec.GetBytes("runcommand\n");
            byte[] argumentBuffer;

            argumentBuffer = command.Aggregate(new List<byte>(), (bytes, arg) => {
                bytes.AddRange(_codec.GetBytes(arg));
                bytes.Add(0);
                return bytes;
            },
            bytes => {
                bytes.RemoveAt(bytes.Count - 1);
                return bytes.ToArray();
            }
            ).ToArray();

            byte[] lengthBuffer = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(argumentBuffer.Length));

            lock (_Process)
            {
                _Process.StandardInput.BaseStream.Write(commandBuffer, 0, commandBuffer.Length);
                _Process.StandardInput.BaseStream.Write(lengthBuffer, 0, lengthBuffer.Length);
                _Process.StandardInput.BaseStream.Write(argumentBuffer, 0, argumentBuffer.Length);
                _Process.StandardInput.BaseStream.Flush();

                return ReadCommandOutputs(command, outputs, inputs);
            }// lock _Process
        }

        private int ReadCommandOutputs(IList<string> command, IDictionary<CommandChannel, Stream> outputs, IDictionary<CommandChannel, Func<uint, byte[]>> inputs)
        {
            try
            {
                while (true)
                {
                    CommandMessage message = ReadMessage();
                    if (CommandChannel.Result == message.Channel)
                        return ReadInt(message.Buffer, 0);

                    if (inputs != null && inputs.ContainsKey(message.Channel))
                    {
                        byte[] sendBuffer = inputs[message.Channel](ReadUint(message.Buffer, 0));
                        if (null == sendBuffer || 0 == sendBuffer.LongLength)
                        {
                        }
                        else
                        {
                        }
                    }
                    if (outputs != null && outputs.ContainsKey(message.Channel))
                    {
                        if (message.Buffer.Length > int.MaxValue)
                        {
                            // .NET hates uints
                            int firstPart = message.Buffer.Length / 2;
                            int secondPart = message.Buffer.Length - firstPart;
                            outputs[message.Channel].Write(message.Buffer, 0, firstPart);
                            outputs[message.Channel].Write(message.Buffer, firstPart, secondPart);
                        }
                        else
                        {
                            outputs[message.Channel].Write(message.Buffer, 0, message.Buffer.Length);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                //					Console.WriteLine (_Process.StandardOutput.ReadToEnd ());
                //					Console.WriteLine (_Process.StandardError.ReadToEnd ());
                Console.WriteLine(string.Join(" ", command.ToArray()));
                Console.WriteLine(ex);
                _Process.StandardOutput.BaseStream.Flush();
                _Process.StandardError.BaseStream.Flush();
                throw;
            }
        }

        internal static uint ReadUint(byte[] buffer, int offset)
        {
            if (null == buffer)
                throw new ArgumentNullException("buffer");
            if (buffer.Length < offset + 4)
                throw new ArgumentOutOfRangeException("offset");

            return (uint)IPAddress.NetworkToHostOrder(BitConverter.ToInt32(buffer, offset));
        }

        CommandMessage ReadMessage()
        {
            byte[] header = new byte[MercurialHeaderLength];
            long bytesRead = 0;

            try
            {
                bytesRead = ReadAll(_Process.StandardOutput.BaseStream, header, 0, MercurialHeaderLength);
            }
            catch (Exception ex)
            {
                throw new ServerException("Error reading from command server", ex);
            }

            if (MercurialHeaderLength != bytesRead)
            {
                throw new ServerException(string.Format("Received malformed header from command server: {0} bytes", bytesRead));
            }

            CommandChannel channel = CommandChannelFromFirstByte(header);
            long messageLength = (long)ReadUint(header, 1);

            if (CommandChannel.Input == channel || CommandChannel.Line == channel)
                return new CommandMessage(channel, messageLength.ToString());

            byte[] messageBuffer = new byte[messageLength];

            try
            {
                if (messageLength > int.MaxValue)
                {
                    // .NET hates uints
                    int firstPart = (int)(messageLength / 2);
                    int secondPart = (int)(messageLength - firstPart);

                    bytesRead = ReadAll(_Process.StandardOutput.BaseStream, messageBuffer, 0, firstPart);
                    if (bytesRead == firstPart)
                    {
                        bytesRead += ReadAll(_Process.StandardOutput.BaseStream, messageBuffer, firstPart, secondPart);
                    }
                }
                else
                {
                    bytesRead = ReadAll(_Process.StandardOutput.BaseStream, messageBuffer, 0, (int)messageLength);
                }
            }
            catch (Exception ex)
            {
                throw new ServerException("Error reading from command server", ex);
            }

            if (bytesRead != messageLength)
            {
                throw new ServerException(string.Format("Error reading from command server: Expected {0} bytes, read {1}", messageLength, bytesRead));
            }

            CommandMessage message = new CommandMessage(CommandChannelFromFirstByte(header), messageBuffer);
            // Console.WriteLine ("READ: {0} {1}", message, message.Message);
            return message;
        }

        internal static CommandChannel CommandChannelFromFirstByte(byte[] header)
        {
            char[] identifier = ASCIIEncoding.ASCII.GetChars(header, 0, 1);

            switch (identifier[0])
            {
                case 'I':
                    return CommandChannel.Input;
                case 'L':
                    return CommandChannel.Line;
                case 'o':
                    return CommandChannel.Output;
                case 'e':
                    return CommandChannel.Error;
                case 'r':
                    return CommandChannel.Result;
                case 'd':
                    return CommandChannel.Debug;
                default:
                    throw new ArgumentException(string.Format("Invalid channel identifier: {0}", identifier[0]), "header");
            }
        }

        static int ReadAll(Stream stream, byte[] buffer, int offset, int length)
        {
            if (null == stream)
                throw new ArgumentNullException("stream");

            int remaining = length;
            int read = 0;

            for (; remaining > 0; offset += read, remaining -= read)
            {
                read = stream.Read(buffer, offset, remaining);
            }

            return length - remaining;
        }


        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            GC.SuppressFinalize(this);
            StopPersistentMercurialClient();
        }

        /// <summary>
        /// Determine if this class is supported by the given repository and current
        /// Mercurial client version.
        /// </summary>
        /// <param name="repositoryPath">
        /// The path to the repository to check supportability for.
        /// </param>
        /// <returns>
        /// <c>true</c> if <see cref="PersistentClient"/> is supported for the
        /// given repository and for the current Mercurial client;
        /// otherwise, <c>false</c>.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// <para><paramref name="repositoryPath"/> is <c>null</c> or empty.</para>
        /// </exception>
        public static bool IsSupported(string repositoryPath)
        {
            if (StringEx.IsNullOrWhiteSpace(repositoryPath))
                throw new ArgumentNullException("repositoryPath");

            if (!Directory.Exists(repositoryPath))
                return false;

            // TODO: Determine if we need to check if the .hg folder is an actual repository
            if (!Directory.Exists(Path.Combine(repositoryPath, ".hg")))
                return false;

            if (ClientExecutable.CurrentVersion < new Version(1, 9, 0, 0))
                return false;

            return true;
        }

        /// <summary>
        /// This spins up a Mercurial client in command server mode for the
        /// repository.
        /// </summary>
        private void StartPersistentMercurialClient()
        {
            var psi = new ProcessStartInfo
            {
                FileName = ClientExecutable.ClientPath,
                WorkingDirectory = _RepositoryPath,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                UseShellExecute = false,
                ErrorDialog = false,
                Arguments = "serve --cmdserver pipe --noninteractive" //--encoding cp1251",
            };
            psi.EnvironmentVariables.Add("LANGUAGE", "EN");
            psi.EnvironmentVariables.Remove("HGENCODING");
            psi.EnvironmentVariables.Add("HGENCODING", ClientExecutable.GetMainEncoding().WebName);

            psi.StandardOutputEncoding = ClientExecutable.GetMainEncoding();
            psi.StandardErrorEncoding = ClientExecutable.GetMainEncoding();

            _Process = Process.Start(psi);
            DecodeInitialBlock();
        }

        /// <summary>
        /// Decodes the initial block that Mercurial outputs when it is spun up.
        /// </summary>
        private void DecodeInitialBlock()
        {
            char type;
            string content;
            int exitCode;
            CommandServerOutputDecoder.DecodeOneBlock(_Process.StandardOutput, out type, out content, out exitCode);
        }

        /// <summary>
        /// This spins down the Mercurial client that was spun up by
        /// <see cref="StartPersistentMercurialClient"/>.
        /// </summary>
        private void StopPersistentMercurialClient()
        {
            if (_Process == null)
                return;

            try
            {
                _Process.StandardInput.Close();
                _Process.WaitForExit();
            }
            catch(ObjectDisposedException)
            {
                Debug.WriteLine("Prevented attempt to close already disposed StandardInput");
            }
            finally
            {
                _Process = null;
            }
            
        }
    }
}
