# CS 3502: Multi-Threaded Programming and IPC Project

This repository contains the implementation for CS 3502 Project 1, focusing on Multi-Threaded Programming and Inter-Process Communication (IPC).

## Project Overview

This project consists of two main components:

### Project A: Multi-Threading Implementation

A banking simulation that demonstrates concurrent programming concepts through four phases implemented in a single codebase:

1. **Phase 1: Basic Thread Operations**
   - Creation and management of multiple threads
   - Simulation of concurrent banking transactions

2. **Phase 2: Resource Protection**
   - Implementation of mutex locks for shared resources
   - Prevention of race conditions in account operations

3. **Phase 3: Deadlock Creation**
   - Demonstration of deadlock scenarios between threads
   - Visualization of deadlock occurrence

4. **Phase 4: Deadlock Resolution**
   - Implementation of deadlock prevention techniques
   - Resource ordering and timeout mechanisms

All four phases are implemented progressively in the same code, with Phase 4 being the complete implementation that includes all previous phases' functionality.

### Project B: Inter-Process Communication (IPC)

A socket-based communication system demonstrating:

- Data transmission between separate processes through TCP/IP sockets
- Structured data exchange using JSON serialization
- Data integrity verification with checksums
- Error handling and recovery mechanisms
- Performance benchmarking

## Dependencies

- **.NET SDK** (6.0 or newer)
- **C# Compiler**
- **Linux Environment** (Ubuntu recommended, or WSL on Windows)

## Installation

### Prerequisites

1. Install .NET SDK:
   ```bash
   sudo apt-get update
   sudo apt-get install -y dotnet-sdk-6.0
   ```

2. Verify installation:
   ```bash
   dotnet --version
   ```

### Setting Up the Project

1. Clone this repository:
   ```bash
   git clone <repository-url>
   cd CS3502_Project
   ```

2. Build the project:
   ```bash
   dotnet build
   ```

## Running Project A: Multi-Threading Banking Simulation

The banking simulation is implemented as a complete solution in Phase4.cs that demonstrates all multi-threading concepts:

```bash
cd ProjectA_Obie/myproject
dotnet run
```

This demonstrates:
- Creation of multiple customer threads
- Mutex locks for protecting shared account resources
- Prevention of race conditions during transactions
- Deadlock prevention with resource ordering
- Timeout mechanisms for lock acquisition

### Testing

The multi-threading implementation includes built-in testing:

```bash
# The testing is part of the main execution
dotnet run
```

This runs:
- Concurrency testing - Verifies threads run simultaneously
- Synchronization validation - Tests mutex effectiveness
- Stress testing - Simulates high-load scenarios

## Running Project B: IPC Socket Demo

The IPC implementation can be run in several modes:

### Basic Operation

Run producer and consumer in separate terminal windows:

Terminal 1 (Consumer):
```bash
cd IPCSocketDemo
dotnet run consumer
```

Terminal 2 (Producer):
```bash
dotnet run producer
```

### Automated Test Modes

For convenience, you can run tests with a single command:

**Data Integrity Test**:
```bash
dotnet run test
```

**Error Handling Test**:
```bash
dotnet run error-test
```

**Performance Benchmark**:
```bash
dotnet run benchmark
```

## Project Structure

```
CS3502_Project/
├── ProjectA_Obie/                   # Multi-threading implementation
│   └── myproject/                   
│       ├── BankingSimulation.cs     # Complete banking simulation with all phases
│       └── myproject.csproj         # Project file
│
├── ProjectB_Obie/                    # IPC implementation
│   └── ipc/ 
│       ├── IPC.cs                    # Socket-based IPC implementation
│       └── ipc.csproj                # Project file
│
└── README.md                        # This file
```

## Implementation Details

### Multi-Threading Implementation (BankingSimulation.cs)

The complete banking simulation demonstrates:

1. **Thread Creation and Management**
   - Creates customer and teller threads to process banking transactions
   - Manages thread lifecycle with start and join operations

2. **Resource Protection**
   - Uses lock statements to protect shared bank account resources
   - Prevents race conditions during deposit, withdraw, and transfer operations

3. **Deadlock Prevention**
   - Implements resource ordering by account ID to prevent circular wait
   - Uses timeouts with Monitor.TryEnter() to prevent indefinite waiting
   - Implements retry mechanisms with exponential backoff

4. **Testing**
   - Verifies thread concurrency by checking overlapping transactions
   - Validates mutex synchronization through high-contention testing
   - Stress tests the system with many concurrent customers

### IPC Implementation

The socket-based IPC demonstrates:

1. **Socket Communication**
   - Uses TCP/IP sockets for reliable communication between processes
   - Implements producer-consumer pattern for data exchange

2. **Data Integrity**
   - Uses JSON serialization for structured data
   - Implements checksums to verify data integrity
   - Tests corruption detection

3. **Error Handling**
   - Handles connection failures, broken pipes, and invalid data
   - Implements reconnection and recovery mechanisms
   - Ensures proper resource cleanup

4. **Performance Measurement**
   - Benchmarks throughput and latency across various payload sizes
   - Measures scaling characteristics of the communication channel

## Testing Overview

### Multi-Threading Tests (in Phase4.cs)

#### Concurrency Testing
Verifies that multiple threads run simultaneously without interfering with each other by checking for overlapping transaction timestamps.

#### Synchronization Validation
Tests mutex effectiveness by creating high-contention scenarios and verifying consistent account balances.

#### Stress Testing
Simulates high-load scenarios with many concurrent customers to evaluate system stability.

### IPC Tests

#### Data Integrity Testing
Verifies that data remains intact during transmission using checksums and validation.

#### Error Handling Validation
Tests application resilience against connection failures, corrupt data, and broken pipes.

#### Performance Benchmarking
Measures throughput (MB/s) and latency (ms) across different message sizes.

## Troubleshooting

### Common Issues

1. **Port in use**: If you get "address already in use" errors, change the port number in the constants.

2. **Connection timeouts**: Ensure both producer and consumer are running and check firewall settings.

3. **Build errors**: Verify you have the correct .NET SDK version installed.

### Debugging

For detailed debugging output:

```bash
# Project A
dotnet run --verbosity detailed

# Project B
dotnet run test --debug
```

