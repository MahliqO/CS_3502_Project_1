using System;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Threading;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace IPCSocketDemo
{
    // Structured data class to pass between processes
    public class TransactionData
    {
        public int Id { get; set; }
        public string Customer { get; set; }
        public decimal Amount { get; set; }
        public string Description { get; set; }
        public DateTime Timestamp { get; set; }
        public string Checksum { get; set; } // For integrity verification
        public string Payload { get; set; } // Additional payload for benchmark tests
    }

    // Benchmark result class
    public class BenchmarkResult
    {
        public int PayloadSize { get; set; }
        public int MessageCount { get; set; }
        public long TotalBytes { get; set; }
        public double ElapsedMilliseconds { get; set; }
        public double ThroughputMBps => (TotalBytes / 1024.0 / 1024.0) / (ElapsedMilliseconds / 1000.0);
        public double MessagesPerSecond => MessageCount / (ElapsedMilliseconds / 1000.0);
        public double AvgLatencyMs => ElapsedMilliseconds / MessageCount;
    }

    class Program
    {
        private const int Port = 8080;
        private const string EndMarker = "DONE";
        private const int NumItems = 10;
        private static CancellationTokenSource cancellationSource;
        
        // Benchmark configuration
        private static readonly int[] PayloadSizes = { 100, 1_000, 10_000, 100_000, 1_000_000 };
        private const int WarmupIterations = 5;
        private const int BenchmarkIterations = 20;

        static void Main(string[] args)
        {
            // Initialize cancellation token source
            cancellationSource = new CancellationTokenSource();
            
            // Handle Ctrl+C to demonstrate graceful shutdown
            Console.CancelKeyPress += (sender, e) => {
                Console.WriteLine("Cancellation requested. Shutting down gracefully...");
                cancellationSource.Cancel();
                e.Cancel = true; // Prevent process termination
            };

            if (args.Length == 0)
            {
                Console.WriteLine("Please specify mode:");
                Console.WriteLine("  producer - Run as data producer");
                Console.WriteLine("  consumer - Run as data consumer");
                Console.WriteLine("  test - Run data integrity test");
                Console.WriteLine("  error-test - Run error handling validation tests");
                Console.WriteLine("  benchmark - Run performance benchmark tests");
                return;
            }

            string mode = args[0].ToLower();
            bool simulateErrors = args.Length > 1 && args[1].ToLower() == "true";

            try
            {
                switch (mode)
                {
                    case "producer":
                        RunProducer(simulateErrors);
                        break;
                    case "consumer":
                        RunConsumer(simulateErrors);
                        break;
                    case "test":
                        RunTest(false);
                        break;
                    case "error-test":
                        RunErrorTest();
                        break;
                    case "benchmark":
                        RunBenchmarkTest();
                        break;
                    case "benchmark-producer":
                        RunBenchmarkProducer();
                        break;
                    case "benchmark-consumer":
                        RunBenchmarkConsumer();
                        break;
                    default:
                        Console.WriteLine("Invalid mode. Please specify a valid mode.");
                        break;
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Operation was canceled.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected error: {ex.GetType().Name} - {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }

        static void RunTest(bool withErrors)
        {
            Console.WriteLine(withErrors ? 
                "Starting Error Handling Test" : 
                "Starting Data Integrity Test");
            Console.WriteLine("============================");
            
            // Start the consumer in a new process
            System.Diagnostics.Process consumerProcess = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = withErrors ? "run consumer true" : "run consumer false",
                    UseShellExecute = true
                }
            };
            consumerProcess.Start();
            
            // Give consumer time to start
            Thread.Sleep(2000);
            
            // Start the producer in this process
            RunProducer(withErrors);
            
            Console.WriteLine("Test completed. Check the consumer window for results.");
        }

        static void RunBenchmarkTest()
        {
            Console.WriteLine("Starting Performance Benchmark Test");
            Console.WriteLine("==================================");
            
            // Start the benchmark consumer in a new process
            System.Diagnostics.Process consumerProcess = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = "run benchmark-consumer",
                    UseShellExecute = true
                }
            };
            consumerProcess.Start();
            
            // Give consumer time to start
            Console.WriteLine("Waiting for benchmark consumer to start...");
            Thread.Sleep(2000);
            
            // Start the benchmark producer in this process
            RunBenchmarkProducer();
            
            Console.WriteLine("Benchmark test completed.");
        }

        static void RunErrorTest()
        {
            Console.WriteLine("=== ERROR HANDLING VALIDATION TESTS ===");
            Console.WriteLine("These tests verify the program's ability to handle:");
            Console.WriteLine("1. Connection failures");
            Console.WriteLine("2. Invalid/corrupt data");
            Console.WriteLine("3. Broken connections mid-communication");
            Console.WriteLine("4. Resource cleanup");
            
            RunTest(true);
        }

        static void RunProducer(bool simulateErrors)
        {
            Console.WriteLine("=== Producer Starting ===");
            Console.WriteLine(simulateErrors ? 
                "ERROR HANDLING TEST: Will simulate errors during transmission" : 
                "DATA INTEGRITY TEST: Sending structured JSON data with checksums");

            TcpListener listener = null;
            
            try
            {
                // Create a TCP/IP socket with timeout
                listener = new TcpListener(IPAddress.Loopback, Port);
                listener.Start();
                Console.WriteLine($"Producer listening on port {Port}");
                Console.WriteLine("Waiting for consumer to connect...");

                // Use timeout for accepting connection
                var acceptTask = listener.AcceptTcpClientAsync();
                if (!acceptTask.Wait(TimeSpan.FromSeconds(30), cancellationSource.Token))
                {
                    throw new TimeoutException("Timeout waiting for consumer to connect");
                }

                using (TcpClient client = acceptTask.Result)
                using (NetworkStream stream = client.GetStream())
                using (StreamWriter writer = new StreamWriter(stream) { AutoFlush = true })
                {
                    Console.WriteLine("Consumer connected!");
                    Random random = new Random();
                    string[] customers = { "Alice", "Bob", "Charlie", "Diana", "Evan" };
                    string[] descriptions = { "Deposit", "Withdrawal", "Transfer", "Payment", "Refund" };

                    // Set timeouts
                    client.SendTimeout = 5000; // 5 seconds
                    client.ReceiveTimeout = 5000; // 5 seconds

                    // Send number of transactions first
                    writer.WriteLine(NumItems.ToString());
                    Console.WriteLine($"Sending {NumItems} transactions");

                    int brokenConnectionAt = simulateErrors ? random.Next(4, 7) : -1;

                    // Generate and send transactions
                    for (int i = 1; i <= NumItems; i++)
                    {
                        // Check for cancellation
                        cancellationSource.Token.ThrowIfCancellationRequested();
                        
                        // Simulate broken connection
                        if (simulateErrors && i == brokenConnectionAt)
                        {
                            Console.WriteLine($"Simulating broken connection at transaction {i}");
                            // Close connection abruptly
                            client.Close();
                            break;
                        }

                        try
                        {
                            // Create transaction data
                            TransactionData transaction = new TransactionData
                            {
                                Id = i,
                                Customer = customers[random.Next(customers.Length)],
                                Amount = Math.Round((decimal)(random.NextDouble() * 1000), 2),
                                Description = descriptions[random.Next(descriptions.Length)],
                                Timestamp = DateTime.Now
                            };

                            // Calculate and add checksum
                            transaction.Checksum = CalculateChecksum(transaction);
                            
                            // Corrupt data or send invalid format occasionally if simulating errors
                            if (simulateErrors)
                            {
                                if (i == 3)
                                {
                                    // Send invalid JSON
                                    Console.WriteLine("Sending invalid JSON format for transaction #3");
                                    writer.WriteLine("{bad json that will fail to parse}");
                                    Thread.Sleep(500);
                                    continue;
                                }
                                else if (i == 5)
                                {
                                    // Corrupt data without updating checksum
                                    Console.WriteLine("Sending corrupt data (invalid checksum) for transaction #5");
                                    transaction.Amount += 0.01m;
                                }
                            }

                            // Serialize to JSON and send
                            string json = JsonSerializer.Serialize(transaction);
                            writer.WriteLine(json);
                            Console.WriteLine($"Produced: Transaction {i} - {transaction.Customer}: ${transaction.Amount} ({transaction.Description})");
                            
                            // Simulate production time
                            Thread.Sleep(500);
                        }
                        catch (IOException ex)
                        {
                            Console.WriteLine($"IO Error sending transaction {i}: {ex.Message}");
                            break;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error creating transaction {i}: {ex.Message}");
                            // Continue with next transaction
                        }
                    }

                    // Send termination signal if we haven't broken the connection
                    if (client.Connected)
                    {
                        writer.WriteLine(EndMarker);
                        Console.WriteLine($"Sent termination signal: {EndMarker}");
                    }
                }
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"Socket Error: {ex.Message} (Error code: {ex.SocketErrorCode})");
                Console.WriteLine("Handled gracefully - resources will be cleaned up");
            }
            catch (IOException ex)
            {
                Console.WriteLine($"IO Error: {ex.Message}");
                Console.WriteLine("Handled gracefully - likely a broken pipe");
            }
            catch (TimeoutException ex)
            {
                Console.WriteLine($"Timeout: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Producer Error: {ex.GetType().Name} - {ex.Message}");
            }
            finally
            {
                // Clean up resources properly
                try
                {
                    listener?.Stop();
                    Console.WriteLine("Socket resources cleaned up properly");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error during cleanup: {ex.Message}");
                }
                
                Console.WriteLine("=== Producer Completed ===");
            }
            
            Console.WriteLine("Press Enter to exit...");
            Console.ReadLine();
        }

        static void RunConsumer(bool expectErrors)
        {
            Console.WriteLine("=== Consumer Starting ===");
            Console.WriteLine(expectErrors ? 
                "ERROR HANDLING TEST: Expecting errors during transmission" : 
                "DATA INTEGRITY TEST: Receiving and verifying JSON data with checksums");
            Console.WriteLine("Connecting to producer...");
            
            TcpClient client = null;
            
            try
            {
                // Create a TCP/IP client with timeout
                client = new TcpClient();
                client.SendTimeout = 5000;  // 5 seconds
                client.ReceiveTimeout = 10000;  // 10 seconds
                
                // Connect with timeout
                IAsyncResult connectResult = client.BeginConnect(IPAddress.Loopback, Port, null, null);
                bool connected = connectResult.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(30));
                
                if (!connected)
                {
                    throw new TimeoutException("Timeout connecting to producer");
                }
                
                // Complete connection
                client.EndConnect(connectResult);
                Console.WriteLine("Connected to producer!");
                
                // Setup recovery variables for error handling validation
                int reconnectAttempts = 0;
                const int maxReconnectAttempts = 3;
                bool needReconnect = false;
                int expectedCount = 0;
                int processedCount = 0;
                int validCount = 0;
                int invalidCount = 0;
                int errorCount = 0;
                
                do
                {
                    needReconnect = false;
                    
                    try
                    {
                        // If this is a reconnect attempt
                        if (reconnectAttempts > 0)
                        {
                            Console.WriteLine($"Reconnection attempt {reconnectAttempts} of {maxReconnectAttempts}...");
                            Thread.Sleep(2000); // Wait before reconnect
                            
                            client = new TcpClient();
                            client.SendTimeout = 5000;
                            client.ReceiveTimeout = 10000;
                            
                            // Attempt to reconnect
                            connectResult = client.BeginConnect(IPAddress.Loopback, Port, null, null);
                            connected = connectResult.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(10));
                            
                            if (!connected)
                            {
                                throw new TimeoutException("Timeout during reconnection attempt");
                            }
                            
                            client.EndConnect(connectResult);
                            Console.WriteLine("Reconnected to producer!");
                        }
                        
                        using (NetworkStream stream = client.GetStream())
                        using (StreamReader reader = new StreamReader(stream))
                        {
                            // First connection: Read expected count
                            if (reconnectAttempts == 0)
                            {
                                string countLine = reader.ReadLine();
                                if (!int.TryParse(countLine, out expectedCount))
                                {
                                    Console.WriteLine("Failed to read transaction count");
                                    return;
                                }
                                Console.WriteLine($"Expecting {expectedCount} transactions");
                            }
                            // Reconnection: Continue from where we left off
                            else
                            {
                                Console.WriteLine($"Continuing from transaction {processedCount+1} of {expectedCount}");
                            }
                            
                            string line;
                            
                            // Process incoming data
                            while ((line = reader.ReadLine()) != null)
                            {
                                // Check for cancellation
                                cancellationSource.Token.ThrowIfCancellationRequested();
                                
                                // Check for termination signal
                                if (line == EndMarker)
                                {
                                    Console.WriteLine("Received termination signal");
                                    break;
                                }

                                processedCount++;
                                
                                try
                                {
                                    // Try to parse the data
                                    TransactionData transaction = JsonSerializer.Deserialize<TransactionData>(line);
                                    
                                    if (transaction == null)
                                    {
                                        Console.WriteLine($"Transaction {processedCount} - Null after deserialization");
                                        invalidCount++;
                                        continue;
                                    }
                                    
                                    // Verify data integrity
                                    bool isValid = VerifyIntegrity(transaction);
                                    
                                    // Display and count results
                                    if (isValid)
                                    {
                                        Console.WriteLine($"Consumed: Transaction {transaction.Id} - {transaction.Customer}: ${transaction.Amount} ({transaction.Description}) - VALID");
                                        validCount++;
                                    }
                                    else
                                    {
                                        Console.WriteLine($"Consumed: Transaction {transaction.Id} - {transaction.Customer}: ${transaction.Amount} ({transaction.Description}) - DATA INTEGRITY ERROR");
                                        invalidCount++;
                                    }
                                }
                                catch (JsonException ex)
                                {
                                    Console.WriteLine($"Transaction {processedCount} - Error parsing JSON: {ex.Message}");
                                    invalidCount++;
                                    errorCount++;
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"Transaction {processedCount} - Unexpected error: {ex.GetType().Name} - {ex.Message}");
                                    errorCount++;
                                }
                                
                                // Simulate processing time
                                Thread.Sleep(300);
                            }
                        }
                    }
                    catch (IOException ex)
                    {
                        Console.WriteLine($"IO Error: {ex.Message}");
                        Console.WriteLine("Connection was broken. This may be an intentional error simulation.");
                        
                        if (expectErrors && reconnectAttempts < maxReconnectAttempts)
                        {
                            reconnectAttempts++;
                            needReconnect = true;
                            Console.WriteLine("Attempting to recover connection...");
                        }
                        else
                        {
                            Console.WriteLine("Max reconnection attempts reached or not in error test mode.");
                        }
                        
                        errorCount++;
                    }
                    catch (SocketException ex)
                    {
                        Console.WriteLine($"Socket Error: {ex.Message} (Error code: {ex.SocketErrorCode})");
                        
                        if (expectErrors && reconnectAttempts < maxReconnectAttempts)
                        {
                            reconnectAttempts++;
                            needReconnect = true;
                            Console.WriteLine("Attempting to recover connection...");
                        }
                        
                        errorCount++;
                    }
                
                } while (needReconnect);
                
                // Report results
                Console.WriteLine("\nTest Results:");
                Console.WriteLine($"Total expected transactions: {expectedCount}");
                Console.WriteLine($"Successfully processed: {processedCount}");
                Console.WriteLine($"Valid transactions: {validCount}");
                Console.WriteLine($"Corrupt transactions detected: {invalidCount}");
                Console.WriteLine($"Connection or parsing errors: {errorCount}");
                Console.WriteLine($"Reconnection attempts: {reconnectAttempts}");
                
                if (expectErrors)
                {
                    Console.WriteLine("\nERROR HANDLING TEST RESULTS:");
                    if (errorCount > 0)
                    {
                        Console.WriteLine("✓ Errors were detected and handled");
                        Console.WriteLine("✓ Application continued execution without crashing");
                        Console.WriteLine($"✓ {processedCount} of {expectedCount} transactions were processed despite errors");
                        
                        if (reconnectAttempts > 0)
                            Console.WriteLine("✓ Connection recovery was attempted");
                    }
                    else
                    {
                        Console.WriteLine("⚠ No errors were encountered during the test");
                    }
                }
                else
                {
                    if (invalidCount == 0 && errorCount == 0)
                        Console.WriteLine("DATA INTEGRITY TEST PASSED: All data was transmitted with integrity maintained");
                    else
                        Console.WriteLine($"DATA INTEGRITY TEST DETECTED ISSUES: {invalidCount + errorCount} transactions had integrity or transmission issues");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Consumer Error: {ex.GetType().Name} - {ex.Message}");
            }
            finally
            {
                // Clean up resources properly
                try
                {
                    client?.Close();
                    Console.WriteLine("Socket resources cleaned up properly");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error during cleanup: {ex.Message}");
                }
                
                Console.WriteLine("=== Consumer Completed ===");
            }
            
            Console.WriteLine("Press Enter to exit...");
            Console.ReadLine();
        }

        // Performance Benchmark Producer
        static void RunBenchmarkProducer()
        {
            Console.WriteLine("=== Benchmark Producer Starting ===");
            Console.WriteLine("This will test throughput and latency for various payload sizes");

            // Create a TCP/IP socket
            TcpListener listener = new TcpListener(IPAddress.Loopback, Port);
            
            try
            {
                // Start listening for client connections
                listener.Start();
                Console.WriteLine($"Benchmark producer listening on port {Port}");
                Console.WriteLine("Waiting for benchmark consumer to connect...");

                // Accept a client connection
                using (TcpClient client = listener.AcceptTcpClient())
                using (NetworkStream stream = client.GetStream())
                using (StreamWriter writer = new StreamWriter(stream) { AutoFlush = true })
                using (StreamReader reader = new StreamReader(stream))
                {
                    Console.WriteLine("Benchmark consumer connected!");

                    // Set buffer sizes for performance
                    client.SendBufferSize = 65536;
                    client.ReceiveBufferSize = 65536;
                    client.NoDelay = true; // Disable Nagle's algorithm for benchmarking

                    // Create a stopwatch for timing
                    Stopwatch stopwatch = new Stopwatch();
                    List<BenchmarkResult> results = new List<BenchmarkResult>();

                    // Run benchmark for each payload size
                    foreach (int payloadSize in PayloadSizes)
                    {
                        Console.WriteLine($"\nBenchmarking payload size: {payloadSize} bytes");
                        
                        // Send benchmark configuration
                        writer.WriteLine($"{payloadSize}|{WarmupIterations}|{BenchmarkIterations}");
                        
                        // Generate payload data - reused for all messages with this size
                        string payload = GeneratePayload(payloadSize);
                        
                        // Wait for consumer to be ready
                        string readySignal = reader.ReadLine();
                        if (readySignal != "READY")
                        {
                            Console.WriteLine("Consumer not ready. Aborting benchmark.");
                            break;
                        }

                        Console.WriteLine("Running warmup iterations...");
                        
                        // Warmup runs
                        for (int i = 0; i < WarmupIterations; i++)
                        {
                            SendBenchmarkMessage(writer, i, payload);
                            string response = reader.ReadLine(); // Wait for acknowledgment
                        }

                        Console.WriteLine("Running benchmark iterations...");
                        
                        // Start timing
                        stopwatch.Restart();
                        
                        // Benchmark runs
                        for (int i = 0; i < BenchmarkIterations; i++)
                        {
                            SendBenchmarkMessage(writer, i, payload);
                            string response = reader.ReadLine(); // Wait for acknowledgment
                        }
                        
                        // Stop timing
                        stopwatch.Stop();
                        
                        // Calculate results
                        long totalBytes = payloadSize * BenchmarkIterations;
                        double elapsedMs = stopwatch.ElapsedMilliseconds;
                        
                        BenchmarkResult result = new BenchmarkResult
                        {
                            PayloadSize = payloadSize,
                            MessageCount = BenchmarkIterations,
                            TotalBytes = totalBytes,
                            ElapsedMilliseconds = elapsedMs
                        };
                        
                        results.Add(result);
                        
                        // Report individual result
                        Console.WriteLine($"Completed benchmark for {payloadSize} bytes:");
                        Console.WriteLine($"  Throughput: {result.ThroughputMBps:F2} MB/s");
                        Console.WriteLine($"  Messages: {result.MessagesPerSecond:F2} msgs/s");
                        Console.WriteLine($"  Avg Latency: {result.AvgLatencyMs:F2} ms/msg");
                    }

                    // Send completion signal
                    writer.WriteLine(EndMarker);
                    
                    // Print overall results
                    Console.WriteLine("\n===== BENCHMARK RESULTS =====");
                    Console.WriteLine("Payload Size | Throughput (MB/s) | Messages/sec | Latency (ms)");
                    Console.WriteLine("-------------|------------------|--------------|------------");
                    
                    foreach (var result in results)
                    {
                        Console.WriteLine($"{result.PayloadSize,12} | {result.ThroughputMBps,16:F2} | {result.MessagesPerSecond,12:F2} | {result.AvgLatencyMs,11:F2}");
                    }
                    
                    // Calculate percentage change between smallest and largest payloads
                    if (results.Count >= 2)
                    {
                        var smallest = results.First();
                        var largest = results.Last();
                        
                        double throughputChange = ((largest.ThroughputMBps / smallest.ThroughputMBps) - 1) * 100;
                        double latencyChange = ((largest.AvgLatencyMs / smallest.AvgLatencyMs) - 1) * 100;
                        
                        Console.WriteLine("\nPerformance Scaling:");
                        Console.WriteLine($"Throughput scaling: {throughputChange:F2}% from {smallest.PayloadSize} to {largest.PayloadSize} bytes");
                        Console.WriteLine($"Latency scaling: {latencyChange:F2}% from {smallest.PayloadSize} to {largest.PayloadSize} bytes");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Benchmark Producer Error: {ex.Message}");
            }
            finally
            {
                // Stop listening
                listener.Stop();
                Console.WriteLine("=== Benchmark Producer Completed ===");
            }
            
            Console.WriteLine("Press Enter to exit...");
            Console.ReadLine();
        }

        // Performance Benchmark Consumer
        static void RunBenchmarkConsumer()
        {
            Console.WriteLine("=== Benchmark Consumer Starting ===");
            Console.WriteLine("Will receive and measure performance of various payload sizes");
            
            try
            {
                // Create a TCP/IP client
                using (TcpClient client = new TcpClient())
                {
                    // Set larger buffer size for better performance
                    client.ReceiveBufferSize = 65536;
                    client.SendBufferSize = 65536;
                    client.NoDelay = true; // Disable Nagle's algorithm
                    
                    // Connect to the producer
                    Console.WriteLine("Connecting to benchmark producer...");
                    IAsyncResult connectResult = client.BeginConnect(IPAddress.Loopback, Port, null, null);
                    
                    // Set timeout
                    bool success = connectResult.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(30));
                    
                    if (!success)
                    {
                        Console.WriteLine("Failed to connect to producer (timeout)");
                        return;
                    }
                    
                    // Complete the connection
                    client.EndConnect(connectResult);
                    
                    Console.WriteLine("Connected to benchmark producer!");
                    
                    using (NetworkStream stream = client.GetStream())
                    using (StreamReader reader = new StreamReader(stream))
                    using (StreamWriter writer = new StreamWriter(stream) { AutoFlush = true })
                    {
                        // Create a stopwatch for timing
                        Stopwatch stopwatch = new Stopwatch();
                        List<BenchmarkResult> results = new List<BenchmarkResult>();
                        
                        string configLine;
                        
                        // Process each benchmark configuration
                        while ((configLine = reader.ReadLine()) != null && configLine != EndMarker)
                        {
                            // Parse configuration
                            string[] config = configLine.Split('|');
                            int payloadSize = int.Parse(config[0]);
                            int warmupIterations = int.Parse(config[1]);
                            int benchmarkIterations = int.Parse(config[2]);
                            
                            Console.WriteLine($"\nReceiving benchmark with payload size: {payloadSize} bytes");
                            Console.WriteLine($"Warmup: {warmupIterations} iterations, Benchmark: {benchmarkIterations} iterations");
                            
                            // Signal ready
                            writer.WriteLine("READY");
                            
                            Console.WriteLine("Running warmup iterations...");
                            
                            // Handle warmup iterations
                            for (int i = 0; i < warmupIterations; i++)
                            {
                                string messageLine = reader.ReadLine();
                                ProcessBenchmarkMessage(messageLine);
                                writer.WriteLine("ACK"); // Acknowledge receipt
                            }
                            
                            Console.WriteLine("Running benchmark iterations...");
                            
                            // Start timing
                            stopwatch.Restart();
                            
                            // Handle benchmark iterations
                            for (int i = 0; i < benchmarkIterations; i++)
                            {
                                string messageLine = reader.ReadLine();
                                ProcessBenchmarkMessage(messageLine);
                                writer.WriteLine("ACK"); // Acknowledge receipt
                            }
                            
                            // Stop timing
                            stopwatch.Stop();
                            
                            // Calculate results
                            long totalBytes = payloadSize * benchmarkIterations;
                            double elapsedMs = stopwatch.ElapsedMilliseconds;
                            
                            BenchmarkResult result = new BenchmarkResult
                            {
                                PayloadSize = payloadSize,
                                MessageCount = benchmarkIterations,
                                TotalBytes = totalBytes,
                                ElapsedMilliseconds = elapsedMs
                            };
                            
                            results.Add(result);
                            
                            // Report individual result
                            Console.WriteLine($"Completed benchmark for {payloadSize} bytes:");
                            Console.WriteLine($"  Throughput: {result.ThroughputMBps:F2} MB/s");
                            Console.WriteLine($"  Messages: {result.MessagesPerSecond:F2} msgs/s");
                            Console.WriteLine($"  Avg Latency: {result.AvgLatencyMs:F2} ms/msg");
                        }
                        
                        // Print overall results
                        Console.WriteLine("\n===== BENCHMARK RESULTS =====");
                        Console.WriteLine("Payload Size | Throughput (MB/s) | Messages/sec | Latency (ms)");
                        Console.WriteLine("-------------|------------------|--------------|------------");
                        
                        foreach (var result in results)
                        {
                            Console.WriteLine($"{result.PayloadSize,12} | {result.ThroughputMBps,16:F2} | {result.MessagesPerSecond,12:F2} | {result.AvgLatencyMs,11:F2}");
                        }
                    }
                }
                
                Console.WriteLine("=== Benchmark Consumer Completed ===");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Benchmark Consumer Error: {ex.Message}");
            }
            
            Console.WriteLine("Press Enter to exit...");
            Console.ReadLine();
        }

        // Helper methods for benchmarking
        static void SendBenchmarkMessage(StreamWriter writer, int id, string payload)
        {
            TransactionData transaction = new TransactionData
            {
                Id = id,
                Customer = "BenchmarkTest",
                Amount = id,
                Description = "Benchmark",
                Timestamp = DateTime.Now,
                Payload = payload
            };
            
            // Calculate and add checksum
            transaction.Checksum = CalculateChecksum(transaction);
            
            // Serialize and send
            string json = JsonSerializer.Serialize(transaction);
            writer.WriteLine(json);
        }
        
        static TransactionData ProcessBenchmarkMessage(string json)
        {
            // Deserialize the message
            TransactionData transaction = JsonSerializer.Deserialize<TransactionData>(json);
            
            // Verify integrity
            bool isValid = VerifyIntegrity(transaction);
            
            // In a real system, we might do more processing here
            return transaction;
        }
        
        static string GeneratePayload(int size)
        {
            // For benchmark purposes, generate a repeating pattern
            StringBuilder sb = new StringBuilder(size);
            const string pattern = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            
            for (int i = 0; i < size; i++)
            {
                sb.Append(pattern[i % pattern.Length]);
            }
            
            return sb.ToString();
        }
        
        // Calculate checksum for data integrity
        static string CalculateChecksum(TransactionData data)
        {
            // Store original checksum and payload
            string originalChecksum = data.Checksum;
            string originalPayload = data.Payload;
            
            // Remove checksum and payload for calculation
            data.Checksum = null;
            data.Payload = null;
            
            // Serialize without checksum and payload
            string json = JsonSerializer.Serialize(data);
            
            // Calculate checksum
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(json);
                byte[] hash = sha256.ComputeHash(bytes);
                string checksum = Convert.ToBase64String(hash);
                
                // Restore original values
                data.Checksum = originalChecksum;
                data.Payload = originalPayload;
                
                return checksum;
            }
        }
        
        // Verify data integrity using checksum
        static bool VerifyIntegrity(TransactionData data)
        {
            // Store received checksum and payload
            string receivedChecksum = data.Checksum;
            string originalPayload = data.Payload;
            
            if (string.IsNullOrEmpty(receivedChecksum))
                return false;
                
            // Calculate checksum of received data
            string calculatedChecksum = CalculateChecksum(data);
            
            // Compare checksums
            return receivedChecksum == calculatedChecksum;
        }
    }
}