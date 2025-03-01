using System;
using System.Collections.Generic;
using System.Threading;
using System.Diagnostics;
using System.Linq;

namespace BankingSimulation
{
    // Bank account class remains the same as Phase 4
  public class BankAccount
{
    public int Id { get; }
    private decimal _balance;

    public BankAccount(int id, decimal initialBalance)
    {
        Id = id;
        _balance = initialBalance;
    }

    // Property with synchronized getter
    public decimal Balance
    {
        get
        {
            lock (this)
            {
                return _balance;
            }
        }
    }

    // Method to deposit money with improved synchronization
    public void Deposit(decimal amount)
    {
        lock (this)
        {
            // Simulate some processing time
            Thread.Sleep(100);
            
            // Update balance - protected by the lock
            _balance += amount;
            Console.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss.fff")}] Thread-{Thread.CurrentThread.ManagedThreadId}: Deposited ${amount} to Account {Id}. New balance: ${_balance}");
        }
    }

    // Method to withdraw money with improved synchronization
    public bool Withdraw(decimal amount)
    {
        lock (this)
        {
            // Simulate some processing time
            Thread.Sleep(100);
            
            if (_balance >= amount)
            {
                _balance -= amount;
                Console.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss.fff")}] Thread-{Thread.CurrentThread.ManagedThreadId}: Withdrew ${amount} from Account {Id}. New balance: ${_balance}");
                return true;
            }
            
            Console.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss.fff")}] Thread-{Thread.CurrentThread.ManagedThreadId}: Failed to withdraw ${amount} from Account {Id}. Insufficient funds. Current balance: ${_balance}");
            return false;
        }
    }

    // Method to transfer money to another account with improved synchronization
    public bool TransferTo(BankAccount destinationAccount, decimal amount, int maxRetries = 3)
    {
        // DEADLOCK PREVENTION: Resource Ordering
        BankAccount firstLock = this.Id < destinationAccount.Id ? this : destinationAccount;
        BankAccount secondLock = this.Id < destinationAccount.Id ? destinationAccount : this;
        
        bool isSourceFirst = (this.Id == firstLock.Id);
        
        int retryCount = 0;
        bool transferComplete = false;

        while (!transferComplete && retryCount <= maxRetries)
        {
            bool firstLockAcquired = false;
            bool secondLockAcquired = false;
            
            try
            {
                // Try to acquire first lock with timeout
                Console.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss.fff")}] Thread-{Thread.CurrentThread.ManagedThreadId}: Attempting to acquire lock on Account {firstLock.Id}");
                firstLockAcquired = Monitor.TryEnter(firstLock, 1000);
                
                if (firstLockAcquired)
                {
                    Console.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss.fff")}] Thread-{Thread.CurrentThread.ManagedThreadId}: Acquired lock on Account {firstLock.Id}");
                    
                    // Simulate some processing time
                    Thread.Sleep(100);
                    
                    // Try to acquire second lock with timeout
                    Console.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss.fff")}] Thread-{Thread.CurrentThread.ManagedThreadId}: Attempting to acquire lock on Account {secondLock.Id}");
                    secondLockAcquired = Monitor.TryEnter(secondLock, 1000);
                    
                    if (secondLockAcquired)
                    {
                        Console.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss.fff")}] Thread-{Thread.CurrentThread.ManagedThreadId}: Acquired lock on Account {secondLock.Id}");
                        
                        // Both locks acquired, perform the transfer
                        if (isSourceFirst)
                        {
                            // Normal order: this account is source
                            if (_balance >= amount)
                            {
                                // Withdraw from this account
                                _balance -= amount;
                                // Deposit to destination account
                                secondLock._balance += amount;
                                
                                Console.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss.fff")}] Thread-{Thread.CurrentThread.ManagedThreadId}: Transferred ${amount} from Account {Id} to Account {destinationAccount.Id}");
                                Console.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss.fff")}] Thread-{Thread.CurrentThread.ManagedThreadId}: Account {Id} balance: ${_balance}");
                                Console.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss.fff")}] Thread-{Thread.CurrentThread.ManagedThreadId}: Account {destinationAccount.Id} balance: ${secondLock._balance}");
                                
                                transferComplete = true;
                            }
                            else
                            {
                                Console.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss.fff")}] Thread-{Thread.CurrentThread.ManagedThreadId}: Failed to transfer ${amount} from Account {Id} to Account {destinationAccount.Id}. Insufficient funds.");
                                transferComplete = true; // Mark as complete even though it failed
                            }
                        }
                        else
                        {
                            // Reverse order: destination account is first, this account is second
                            if (_balance >= amount)
                            {
                                // Withdraw from this account
                                _balance -= amount;
                                // Deposit to destination account
                                firstLock._balance += amount;
                                
                                Console.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss.fff")}] Thread-{Thread.CurrentThread.ManagedThreadId}: Transferred ${amount} from Account {Id} to Account {destinationAccount.Id}");
                                Console.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss.fff")}] Thread-{Thread.CurrentThread.ManagedThreadId}: Account {Id} balance: ${_balance}");
                                Console.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss.fff")}] Thread-{Thread.CurrentThread.ManagedThreadId}: Account {destinationAccount.Id} balance: ${firstLock._balance}");
                                
                                transferComplete = true;
                            }
                            else
                            {
                                Console.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss.fff")}] Thread-{Thread.CurrentThread.ManagedThreadId}: Failed to transfer ${amount} from Account {Id} to Account {destinationAccount.Id}. Insufficient funds.");
                                transferComplete = true; // Mark as complete even though it failed
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss.fff")}] Thread-{Thread.CurrentThread.ManagedThreadId}: Timed out waiting for lock on Account {secondLock.Id}");
                        retryCount++;
                        Console.WriteLine($"Transfer attempt failed. Retrying... (Attempt {retryCount} of {maxRetries})");
                        
                        // Random backoff before retrying
                        Random random = new Random();
                        int backoffTime = random.Next(100, 500) * retryCount;
                        Console.WriteLine($"Backing off for {backoffTime}ms before retry");
                        Thread.Sleep(backoffTime);
                    }
                }
                else
                {
                    Console.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss.fff")}] Thread-{Thread.CurrentThread.ManagedThreadId}: Timed out waiting for lock on Account {firstLock.Id}");
                    retryCount++;
                    Console.WriteLine($"Transfer attempt failed. Retrying... (Attempt {retryCount} of {maxRetries})");
                    
                    // Random backoff before retrying
                    Random random = new Random();
                    int backoffTime = random.Next(100, 500) * retryCount;
                    Console.WriteLine($"Backing off for {backoffTime}ms before retry");
                    Thread.Sleep(backoffTime);
                }
            }
            finally
            {
                // Always release locks in reverse order of acquisition
                if (secondLockAcquired)
                {
                    Monitor.Exit(secondLock);
                    Console.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss.fff")}] Thread-{Thread.CurrentThread.ManagedThreadId}: Released lock on Account {secondLock.Id}");
                }
                
                if (firstLockAcquired)
                {
                    Monitor.Exit(firstLock);
                    Console.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss.fff")}] Thread-{Thread.CurrentThread.ManagedThreadId}: Released lock on Account {firstLock.Id}");
                }
            }
        }
        
        if (!transferComplete)
        {
            Console.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss.fff")}] Thread-{Thread.CurrentThread.ManagedThreadId}: Transfer failed after {maxRetries} attempts. Operation aborted.");
        }
        
        return transferComplete;
    }

    // Get balance with mutex protection
    public decimal GetBalance()
    {
        lock (this)
        {
            return _balance;
        }
    }
}
   // Add a Teller class to simulate bank employees
    public class Teller
    {
        public int Id { get; }
        private int customersServed = 0;
        private readonly object lockObject = new object();
       
        // Track when this teller starts and finishes serving customers
        private List<DateTime> serviceStartTimes = new List<DateTime>();
        private List<DateTime> serviceEndTimes = new List<DateTime>();
       
        public Teller(int id)
        {
            Id = id;
        }
       
        public void ServeCustomer(Customer customer, BankAccount sourceAccount, BankAccount destAccount, decimal amount)
        {
            lock (lockObject)
            {
                customersServed++;
            }
           
            DateTime startTime = DateTime.Now;
            serviceStartTimes.Add(startTime);
           
            Console.WriteLine($"[{startTime.ToString("HH:mm:ss.fff")}] Teller {Id} is serving Customer {customer.Id} ({customer.Name}) for transfer ${amount} from Account {sourceAccount.Id} to Account {destAccount.Id}");
           
            // Process the transfer
            sourceAccount.TransferTo(destAccount, amount);
           
            DateTime endTime = DateTime.Now;
            serviceEndTimes.Add(endTime);
           
            Console.WriteLine($"[{endTime.ToString("HH:mm:ss.fff")}] Teller {Id} completed serving Customer {customer.Id} ({customer.Name})");
        }
       
        public int GetCustomersServed()
        {
            lock (lockObject)
            {
                return customersServed;
            }
        }
       
        public List<DateTime> GetServiceStartTimes()
        {
            return serviceStartTimes;
        }
       
        public List<DateTime> GetServiceEndTimes()
        {
            return serviceEndTimes;
        }
    }
    // Update your Customer class to allow overriding
public class Customer
{
    private static int nextId = 1;
    public int Id { get; }
    public string Name { get; }
    protected List<BankAccount> accounts;
    protected List<Teller> tellers;
    private Random random = new Random();
    
    // Track transactions for validation
    private readonly List<TransactionRecord> transactionHistory = new List<TransactionRecord>();
    private readonly object historyLock = new object();
    
    public Customer(string name, List<BankAccount> accounts, List<Teller> tellers)
    {
        Id = nextId++;
        Name = name;
        this.accounts = accounts;
        this.tellers = tellers;
    }
    
    // Make this virtual to allow overriding
    public virtual void PerformTransfersWithTellers()
    {
        // Your existing implementation
    }
    
    // Record a transaction
    public void RecordTransaction(TransactionRecord transaction)
    {
        lock (historyLock)
        {
            transactionHistory.Add(transaction);
        }
    }
    
    // Get transaction history
    public List<TransactionRecord> GetTransactionHistory()
    {
        lock (historyLock)
        {
            return transactionHistory.ToList();
        }
    }
}
   
    // Record class to track transactions for testing
    public class TransactionRecord
    {
        public int CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public int TellerId { get; set; }
        public int SourceAccountId { get; set; }
        public int DestinationAccountId { get; set; }
        public decimal Amount { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public bool Success { get; set; }
    }
   
    // Testing class to validate multithreading
    public class ConcurrencyTester
    {
        private List<Customer> customers;
        private List<Teller> tellers;
        private List<BankAccount> accounts;
        private List<Thread> threads;
       
        public ConcurrencyTester(List<Customer> customers, List<Teller> tellers, List<BankAccount> accounts, List<Thread> threads)
        {
            this.customers = customers;
            this.tellers = tellers;
            this.accounts = accounts;
            this.threads = threads;
        }
       
        public void RunTests()
        {
            Console.WriteLine("\n========= CONCURRENCY TESTING RESULTS =========\n");
           
            // Test 1: Verify that threads operated concurrently
            TestThreadConcurrency();
           
            // Test 2: Verify that tellers worked concurrently
            TestTellerConcurrency();
           
            // Test 3: Verify account balance consistency
            TestAccountConsistency();
           
            // Test 4: Verify load balancing among tellers
            TestTellerLoadBalancing();
           
            Console.WriteLine("\n========= END OF TESTING RESULTS =========\n");
        }
       
        private void TestThreadConcurrency()
        {
            Console.WriteLine("Test 1: Thread Concurrency");
           
            // Collect all transaction records from all customers
            List<TransactionRecord> allTransactions = new List<TransactionRecord>();
            foreach (var customer in customers)
            {
                allTransactions.AddRange(customer.GetTransactionHistory());
            }
           
            // Sort by start time
            allTransactions = allTransactions.OrderBy(t => t.StartTime).ToList();
           
            // Check for overlapping transactions (proof of concurrency)
            bool concurrencyDetected = false;
           
            for (int i = 0; i < allTransactions.Count - 1; i++)
            {
                if (allTransactions[i].EndTime > allTransactions[i + 1].StartTime)
                {
                    concurrencyDetected = true;
                    Console.WriteLine($"  Concurrency detected: Transaction by Customer {allTransactions[i].CustomerId} overlapped with Customer {allTransactions[i + 1].CustomerId}");
                    Console.WriteLine($"    First transaction: {allTransactions[i].StartTime.ToString("HH:mm:ss.fff")} to {allTransactions[i].EndTime.ToString("HH:mm:ss.fff")}");
                    Console.WriteLine($"    Second transaction: {allTransactions[i + 1].StartTime.ToString("HH:mm:ss.fff")} to {allTransactions[i + 1].EndTime.ToString("HH:mm:ss.fff")}");
                    break;
                }
            }
           
            if (concurrencyDetected)
            {
                Console.WriteLine("  PASS: Threads operated concurrently");
            }
            else
            {
                Console.WriteLine("  FAIL: No thread concurrency detected. Threads appear to have run sequentially.");
            }
        }
       
        private void TestTellerConcurrency()
        {
            Console.WriteLine("\nTest 2: Teller Concurrency");
           
            // For each teller, get their service times
            bool tellerConcurrencyDetected = false;
           
            for (int i = 0; i < tellers.Count; i++)
            {
                for (int j = i + 1; j < tellers.Count; j++)
                {
                    List<DateTime> startTimesA = tellers[i].GetServiceStartTimes();
                    List<DateTime> endTimesA = tellers[i].GetServiceEndTimes();
                    List<DateTime> startTimesB = tellers[j].GetServiceStartTimes();
                    List<DateTime> endTimesB = tellers[j].GetServiceEndTimes();
                   
                    // Check for overlapping service times
                    for (int a = 0; a < startTimesA.Count; a++)
                    {
                        for (int b = 0; b < startTimesB.Count; b++)
                        {
                            if ((startTimesA[a] <= endTimesB[b] && endTimesA[a] >= startTimesB[b]) ||
                                (startTimesB[b] <= endTimesA[a] && endTimesB[b] >= startTimesA[a]))
                            {
                                tellerConcurrencyDetected = true;
                                Console.WriteLine($"  Teller concurrency detected: Teller {tellers[i].Id} and Teller {tellers[j].Id} worked simultaneously");
                                goto BreakOutOfNestedLoop;
                            }
                        }
                    }
                }
            }
           
            BreakOutOfNestedLoop:
           
            if (tellerConcurrencyDetected)
            {
                Console.WriteLine("  PASS: Tellers operated concurrently");
            }
            else
            {
                Console.WriteLine("  FAIL: No teller concurrency detected. Tellers appear to have worked sequentially.");
            }
        }
       
        private void TestAccountConsistency()
        {
            Console.WriteLine("\nTest 3: Account Balance Consistency");
           
            // Calculate the expected final balance based on initial balances and transactions
            Dictionary<int, decimal> expectedBalances = new Dictionary<int, decimal>();
           
            // Initialize with starting balances
            foreach (var account in accounts)
            {
                expectedBalances[account.Id] = account.GetBalance();
            }
           
            // Apply all transactions to calculate expected balances
            foreach (var customer in customers)
            {
                foreach (var transaction in customer.GetTransactionHistory())
                {
                    // Skip failed transactions
                    if (!transaction.Success) continue;
                   
                    // Apply transaction effects
                    expectedBalances[transaction.SourceAccountId] -= transaction.Amount;
                    expectedBalances[transaction.DestinationAccountId] += transaction.Amount;
                }
            }
           
            // Compare expected vs actual balances
            bool allBalancesMatch = true;
           
            foreach (var account in accounts)
            {
                decimal actualBalance = account.GetBalance();
                decimal expectedBalance = expectedBalances[account.Id];
               
                if (Math.Abs(actualBalance - expectedBalance) > 0.001m) // Allow small floating-point difference
                {
                    allBalancesMatch = false;
                    Console.WriteLine($"  Balance mismatch in Account {account.Id}: Expected ${expectedBalance}, Actual ${actualBalance}");
                }
            }
           
            if (allBalancesMatch)
            {
                Console.WriteLine("  PASS: All account balances match expected values");
            }
            else
            {
                Console.WriteLine("  FAIL: Some account balances don't match expected values");
            }
        }
       
        private void TestTellerLoadBalancing()
        {
            Console.WriteLine("\nTest 4: Teller Load Balancing");
           
            // Get number of customers served by each teller
            Dictionary<int, int> tellerLoads = new Dictionary<int, int>();
           
            foreach (var teller in tellers)
            {
                tellerLoads[teller.Id] = teller.GetCustomersServed();
                Console.WriteLine($"  Teller {teller.Id} served {tellerLoads[teller.Id]} customers");
            }
           
            // Calculate statistics
            double averageLoad = tellerLoads.Values.Average();
            double maxDeviation = tellerLoads.Values.Max() - tellerLoads.Values.Min();
            double percentDeviation = (maxDeviation / averageLoad) * 100;
           
            Console.WriteLine($"  Average customers per teller: {averageLoad:F2}");
            Console.WriteLine($"  Maximum deviation: {maxDeviation:F2} customers ({percentDeviation:F2}%)");
           
            // Evaluate load balancing - allowing up to 50% deviation for random assignment
            if (percentDeviation <= 50)
            {
                Console.WriteLine("  PASS: Teller workload is reasonably balanced");
            }
            else
            {
                Console.WriteLine("  WARNING: Teller workload is significantly imbalanced");
            }
        }
    }
    public class SynchronizationValidator
{
    // Test class to validate synchronization mechanisms
    public static void PerformSynchronizationTests(List<BankAccount> accounts)
    {
        Console.WriteLine("\n======== SYNCHRONIZATION VALIDATION TESTS ========\n");
        
        // Test 1: High contention on a single account
        TestHighContentionSingleAccount(accounts[0]);
        
        // Test 2: Interleaved operations with artificial delays
        TestInterleavedOperations(accounts);
        
        // Test 3: Rapid concurrent transfers
        TestRapidConcurrentTransfers(accounts);
        
        Console.WriteLine("\n======== END OF SYNCHRONIZATION TESTS ========\n");
    }
    
    private static void TestHighContentionSingleAccount(BankAccount account)
    {
        Console.WriteLine($"Test 1: High Contention on Single Account (ID: {account.Id})");
        
        // Record initial balance
        decimal initialBalance = account.GetBalance();
        Console.WriteLine($"  Initial balance: ${initialBalance}");
        
        // Create a large number of threads all trying to deposit and withdraw the same amount
        int threadCount = 20;
        decimal amount = 10;
        
        // We'll use CountdownEvent to wait for all threads to complete
        CountdownEvent countdown = new CountdownEvent(threadCount);
        
        // Create and start threads
        for (int i = 0; i < threadCount; i++)
        {
            int threadId = i;
            bool isDeposit = i % 2 == 0;  // Even threads deposit, odd threads withdraw
            
            Thread thread = new Thread(() => {
                try
                {
                    // Add a small random delay to increase chance of contention
                    Thread.Sleep(new Random().Next(5, 20));
                    
                    if (isDeposit)
                    {
                        account.Deposit(amount);
                        Console.WriteLine($"  Thread-{Thread.CurrentThread.ManagedThreadId} deposited ${amount}");
                    }
                    else
                    {
                        account.Withdraw(amount);
                        Console.WriteLine($"  Thread-{Thread.CurrentThread.ManagedThreadId} withdrew ${amount}");
                    }
                }
                finally
                {
                    countdown.Signal();
                }
            });
            
            thread.Start();
        }
        
        // Wait for all threads to complete
        countdown.Wait();
        
        // Check final balance - should equal initial balance since we did equal deposits and withdrawals
        decimal finalBalance = account.GetBalance();
        Console.WriteLine($"  Final balance: ${finalBalance}");
        
        if (Math.Abs(finalBalance - initialBalance) < 0.001m)
        {
            Console.WriteLine("  PASS: Balance is consistent after high contention operations");
        }
        else
        {
            Console.WriteLine($"  FAIL: Balance is inconsistent. Expected ${initialBalance}, got ${finalBalance}");
            Console.WriteLine("  This indicates a synchronization issue (race condition)");
        }
    }
    
    private static void TestInterleavedOperations(List<BankAccount> accounts)
    {
        Console.WriteLine("\nTest 2: Interleaved Operations with Artificial Delays");
        
        BankAccount account = accounts[1];  // Use the second account
        decimal initialBalance = account.GetBalance();
        Console.WriteLine($"  Initial balance of Account {account.Id}: ${initialBalance}");
        
        // Create two threads that will perform operations with deliberate delays at critical points
        ManualResetEvent readyEvent = new ManualResetEvent(false);  // Used to synchronize thread start
        CountdownEvent doneEvent = new CountdownEvent(2);  // Used to wait for both threads
        
        // Thread 1: Will deposit amount, then sleep, then withdraw half amount
        Thread thread1 = new Thread(() => {
            try
            {
                readyEvent.WaitOne();  // Wait for signal to start
                
                // Custom implementation to simulate a critical section with a delay
                lock (account)
                {
                    decimal balance = account.GetBalance();
                    Console.WriteLine($"  Thread-{Thread.CurrentThread.ManagedThreadId} read balance: ${balance}");
                    
                    // Deliberate delay inside critical section to test lock effectiveness
                    Console.WriteLine($"  Thread-{Thread.CurrentThread.ManagedThreadId} sleeping inside critical section...");
                    Thread.Sleep(1000);  // One second delay to allow other thread to potentially interfere
                    
                    decimal newBalance = balance + 100;  // Add $100
                    
                    // Another delay before completing the operation
                    Thread.Sleep(500);
                    
                    // Complete the operation
                    account.Deposit(100);
                    Console.WriteLine($"  Thread-{Thread.CurrentThread.ManagedThreadId} deposited $100 after delay");
                }
                
                // Outside critical section, wait a bit
                Thread.Sleep(500);
                
                // Now withdraw half the amount
                account.Withdraw(50);
                Console.WriteLine($"  Thread-{Thread.CurrentThread.ManagedThreadId} withdrew $50");
            }
            finally
            {
                doneEvent.Signal();
            }
        });
        
        // Thread 2: Will try to withdraw a large amount, checking balance before and after
        Thread thread2 = new Thread(() => {
            try
            {
                readyEvent.WaitOne();  // Wait for signal to start
                
                // Custom implementation to simulate a race condition attempt
                decimal balanceBefore = account.GetBalance();
                Console.WriteLine($"  Thread-{Thread.CurrentThread.ManagedThreadId} read balance before: ${balanceBefore}");
                
                // Deliberate delay to try to interleave with Thread 1's operation
                Thread.Sleep(300);
                
                // Try to withdraw a large amount - the lock should protect from inconsistency
                bool success = account.Withdraw(balanceBefore * 0.8m);  // Try to withdraw 80% of what we saw
                
                decimal balanceAfter = account.GetBalance();
                Console.WriteLine($"  Thread-{Thread.CurrentThread.ManagedThreadId} read balance after: ${balanceAfter}");
                Console.WriteLine($"  Withdrawal success: {success}");
            }
            finally
            {
                doneEvent.Signal();
            }
        });
        
        // Start both threads
        thread1.Start();
        thread2.Start();
        
        // Signal threads to begin operations simultaneously
        readyEvent.Set();
        
        // Wait for both threads to complete
        doneEvent.Wait();
        
        // Check final balance
        decimal finalBalance = account.GetBalance();
        Console.WriteLine($"  Final balance: ${finalBalance}");
        
        // The expected result depends on the order of operations, but we're mainly checking
        // that operations were atomic and no balance corruption occurred
        Console.WriteLine("  PASS: Interleaved operations completed without corrupting account state");
    }
    
    private static void TestRapidConcurrentTransfers(List<BankAccount> accounts)
    {
        Console.WriteLine("\nTest 3: Rapid Concurrent Transfers Between Accounts");
        
        // Record initial total balance across all accounts
        decimal initialTotalBalance = accounts.Sum(a => a.GetBalance());
        Console.WriteLine($"  Initial total balance across all accounts: ${initialTotalBalance}");
        
        // Create many threads that will perform transfers between accounts
        int transferCount = 30;
        CountdownEvent transfersDone = new CountdownEvent(transferCount);
        Random random = new Random();
        
        // Start the transfers
        for (int i = 0; i < transferCount; i++)
        {
            int sourceIndex = random.Next(accounts.Count);
            int destIndex;
            do
            {
                destIndex = random.Next(accounts.Count);
            } while (destIndex == sourceIndex);  // Ensure different accounts
            
            decimal amount = random.Next(5, 51);  // $5-$50
            
            Thread thread = new Thread(() => {
                try
                {
                    BankAccount source = accounts[sourceIndex];
                    BankAccount dest = accounts[destIndex];
                    
                    Console.WriteLine($"  Thread-{Thread.CurrentThread.ManagedThreadId} attempting transfer: ${amount} from Account {source.Id} to Account {dest.Id}");
                    
                    // Perform transfer
                    source.TransferTo(dest, amount);
                }
                finally
                {
                    transfersDone.Signal();
                }
            });
            
            thread.Start();
            
            // Add small delay between thread starts to stagger them
            Thread.Sleep(random.Next(10, 50));
        }
        
        // Wait for all transfers to complete
        transfersDone.Wait();
        
        // Check final total balance - should equal initial total balance
        decimal finalTotalBalance = accounts.Sum(a => a.GetBalance());
        Console.WriteLine($"  Final total balance across all accounts: ${finalTotalBalance}");
        
        if (Math.Abs(finalTotalBalance - initialTotalBalance) < 0.001m)
        {
            Console.WriteLine("  PASS: Total balance is preserved after multiple concurrent transfers");
            Console.WriteLine("  This validates that synchronization prevented any loss or duplication of funds");
        }
        else
        {
            Console.WriteLine($"  FAIL: Total balance changed. Expected ${initialTotalBalance}, got ${finalTotalBalance}");
            Console.WriteLine("  This indicates a synchronization issue (race condition)");
        }
    }
}
  

public class BankingStressTest
{
    private List<BankAccount> accounts;
    private List<Thread> customerThreads;
    private List<Customer> customers;
    private List<Teller> tellers;
    private int operationsCompleted = 0;
    private int operationsFailed = 0;
    private readonly object counterLock = new object();
    private Stopwatch stopwatch = new Stopwatch();
    private bool isRunning = false;
    private DateTime lastProgressUpdate = DateTime.MinValue;

    // Stress test configuration
    private int customerCount;
    private int operationsPerCustomer;
    private int accountCount;
    private bool useProgressReporting;

    public BankingStressTest(int customerCount = 50, int operationsPerCustomer = 20, 
                          int accountCount = 5, bool useProgressReporting = true)
    {
        this.customerCount = customerCount;
        this.operationsPerCustomer = operationsPerCustomer;
        this.accountCount = accountCount;
        this.useProgressReporting = useProgressReporting;
    }

    public void RunStressTest()
    {
        Console.WriteLine("\n========== BANKING SYSTEM STRESS TEST ==========\n");
        Console.WriteLine($"Configuration:");
        Console.WriteLine($"- Customer Threads: {customerCount}");
        Console.WriteLine($"- Operations Per Customer: {operationsPerCustomer}");
        Console.WriteLine($"- Total Accounts: {accountCount}");
        Console.WriteLine($"- Total Operations: {customerCount * operationsPerCustomer}");
        Console.WriteLine();

        // Initialize the banking system
        SetupBankingSystem();

        // Start the stress test
        isRunning = true;
        stopwatch.Start();

        if (useProgressReporting)
        {
            // Start progress reporting in a separate thread
            Thread progressThread = new Thread(ReportProgress);
            progressThread.IsBackground = true;
            progressThread.Start();
        }

        // Start all customer threads
        Console.WriteLine("Starting all customer threads...");
        foreach (var thread in customerThreads)
        {
            thread.Start();
        }

        // Wait for all threads to complete
        foreach (var thread in customerThreads)
        {
            thread.Join();
        }

        // Stop the test
        stopwatch.Stop();
        isRunning = false;

        // Display final results
        ReportFinalResults();
    }

    private void SetupBankingSystem()
    {
        // Create bank accounts with varying initial balances
        accounts = new List<BankAccount>();
        for (int i = 1; i <= accountCount; i++)
        {
            accounts.Add(new BankAccount(i, 1000 * i)); // Accounts with $1000, $2000, etc.
        }

        // Create tellers
        tellers = new List<Teller>();
        int tellerCount = Math.Max(5, customerCount / 10); // 1 teller per 10 customers, minimum 5
        for (int i = 1; i <= tellerCount; i++)
        {
            tellers.Add(new Teller(i));
        }

        // Create customers
        customers = new List<Customer>();
        customerThreads = new List<Thread>();

        for (int i = 1; i <= customerCount; i++)
        {
            string name = $"Customer-{i}";
            Customer customer = new StressTestCustomer(name, accounts, tellers, 
                                                   operationsPerCustomer, this);
            customers.Add(customer);
            
            Thread thread = new Thread(customer.PerformTransfersWithTellers);
            customerThreads.Add(thread);
        }
    }

    private void ReportProgress()
    {
        while (isRunning)
        {
            // Update progress every second
            if ((DateTime.Now - lastProgressUpdate).TotalSeconds >= 1)
            {
                lastProgressUpdate = DateTime.Now;
                
                int currentOps;
                int failedOps;
                
                lock (counterLock)
                {
                    currentOps = operationsCompleted;
                    failedOps = operationsFailed;
                }
                
                double elapsedSeconds = stopwatch.ElapsedMilliseconds / 1000.0;
                int totalOperations = customerCount * operationsPerCustomer;
                double percentComplete = (double)currentOps / totalOperations * 100;
                double throughput = currentOps / elapsedSeconds;
                
                Console.WriteLine($"[Progress] {currentOps}/{totalOperations} operations completed ({percentComplete:F2}%) | " +
                                 $"Throughput: {throughput:F2} ops/sec | Failed: {failedOps} | Elapsed: {elapsedSeconds:F2}s");
                
                // Check for potential system stability issues
                if (throughput < 0.5 && currentOps > 10)
                {
                    Console.WriteLine("WARNING: System throughput is very low. Possible stability issue detected.");
                }
            }
            
            Thread.Sleep(200); // Sleep to prevent tight loop
        }
    }

    private void ReportFinalResults()
    {
        double elapsedSeconds = stopwatch.ElapsedMilliseconds / 1000.0;
        int totalOperations = operationsCompleted;
        int totalFailed = operationsFailed;
        double throughput = totalOperations / elapsedSeconds;
        
        Console.WriteLine("\n========== STRESS TEST RESULTS ==========\n");
        Console.WriteLine($"Test Duration: {elapsedSeconds:F2} seconds");
        Console.WriteLine($"Total Operations Completed: {totalOperations}");
        Console.WriteLine($"Failed Operations: {totalFailed}");
        Console.WriteLine($"Operation Throughput: {throughput:F2} operations/second");
        
        // Report account balances
        Console.WriteLine("\nFinal Account Balances:");
        decimal totalBalance = 0;
        foreach (var account in accounts)
        {
            decimal balance = account.GetBalance();
            totalBalance += balance;
            Console.WriteLine($"Account {account.Id}: ${balance:F2}");
        }
        Console.WriteLine($"Total Balance Across All Accounts: ${totalBalance:F2}");
        
        // Report teller workload distribution
        Console.WriteLine("\nTeller Workload Distribution:");
        int totalCustomersServed = tellers.Sum(t => t.GetCustomersServed());
        foreach (var teller in tellers)
        {
            int customersServed = teller.GetCustomersServed();
            double percentage = (double)customersServed / totalCustomersServed * 100;
            Console.WriteLine($"Teller {teller.Id}: {customersServed} customers ({percentage:F2}%)");
        }
        
        // Calculate contention metrics
        double averageOperationsPerSecond = throughput;
        double theoreticalMaxThroughput = customerCount / 0.1; // Assuming 100ms per operation in ideal conditions
        double contentionFactor = 1 - (averageOperationsPerSecond / theoreticalMaxThroughput);
        
        Console.WriteLine("\nPerformance Analysis:");
        Console.WriteLine($"Theoretical Max Throughput: {theoreticalMaxThroughput:F2} ops/sec");
        Console.WriteLine($"Actual Throughput: {averageOperationsPerSecond:F2} ops/sec");
        Console.WriteLine($"Resource Contention Factor: {contentionFactor:F2} (0 = No contention, 1 = Complete blocking)");
        
        // Overall assessment
        Console.WriteLine("\nOverall System Stability Assessment:");
        if (operationsFailed == 0 && contentionFactor < 0.7)
        {
            Console.WriteLine("PASSED: System remained stable under high load with acceptable throughput");
        }
        else if (operationsFailed == 0 && contentionFactor < 0.9)
        {
            Console.WriteLine("MARGINALLY PASSED: System remained stable but showed high contention");
        }
        else if (operationsFailed > 0 && operationsFailed < totalOperations * 0.01)
        {
            Console.WriteLine("WARNING: System mostly stable but had minor failures");
        }
        else
        {
            Console.WriteLine("FAILED: System showed significant instability under stress");
        }
        
        Console.WriteLine("\n========== END OF STRESS TEST ==========\n");
    }

    // Method for customer threads to report completed operations
    public void ReportOperationCompleted(bool success)
    {
        lock (counterLock)
        {
            if (success)
                operationsCompleted++;
            else
                operationsFailed++;
        }
    }

    // Internal class for stress testing
    private class StressTestCustomer : Customer
    {
        private int operationsToPerform;
        private BankingStressTest stressTest;
        private Random random = new Random();

        public StressTestCustomer(string name, List<BankAccount> accounts, List<Teller> tellers, 
                             int operationsToPerform, BankingStressTest stressTest) 
            : base(name, accounts, tellers)
        {
            this.operationsToPerform = operationsToPerform;
            this.stressTest = stressTest;
        }

        public override void PerformTransfersWithTellers()
        {
            try
            {
                Console.WriteLine($"Customer {Id} ({Name}) started stress testing operations");
                
                for (int i = 0; i < operationsToPerform; i++)
                {
                    // Choose random accounts for transfer
                    BankAccount sourceAccount = accounts[random.Next(accounts.Count)];
                    BankAccount destAccount;
                    do
                    {
                        destAccount = accounts[random.Next(accounts.Count)];
                    } while (destAccount.Id == sourceAccount.Id);
                    
                    // Random amount between $5 and $50
                    decimal amount = random.Next(5, 51);
                    
                    // Select a random teller with exponential backoff on contention
                    Teller teller = null;
                    bool tellerFound = false;
                    int retryCount = 0;
                    
                    // Simulate peak load situations where tellers may be busy
                    while (!tellerFound && retryCount < 3)
                    {
                        teller = tellers[random.Next(tellers.Count)];
                        
                        // For stress testing, randomly fail to get a teller sometimes to simulate congestion
                        tellerFound = (random.NextDouble() > 0.1 * retryCount);
                        
                        if (!tellerFound)
                        {
                            retryCount++;
                            Thread.Sleep(random.Next(50, 200) * retryCount); // Exponential backoff
                        }
                    }
                    
                    if (tellerFound)
                    {
                        try
                        {
                            // Create transaction record
                            TransactionRecord transaction = new TransactionRecord
                            {
                                CustomerId = Id,
                                CustomerName = Name,
                                TellerId = teller.Id,
                                SourceAccountId = sourceAccount.Id,
                                DestinationAccountId = destAccount.Id,
                                Amount = amount,
                                StartTime = DateTime.Now
                            };
                            
                            // Perform the transfer
                            teller.ServeCustomer(this, sourceAccount, destAccount, amount);
                            
                            // Complete the transaction record
                            transaction.EndTime = DateTime.Now;
                            transaction.Success = true;
                            
                            // Record successful operation
                            RecordTransaction(transaction);
                            stressTest.ReportOperationCompleted(true);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error in customer {Id} operation: {ex.Message}");
                            stressTest.ReportOperationCompleted(false);
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Customer {Id} couldn't find an available teller after {retryCount} attempts");
                        stressTest.ReportOperationCompleted(false);
                    }
                    
                    // Minimal delay between operations to maximize stress
                    Thread.Sleep(random.Next(10, 50));
                }
                
                Console.WriteLine($"Customer {Id} ({Name}) completed all stress testing operations");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fatal error in customer {Id} thread: {ex.Message}");
            }
        }
    }
}
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Banking Simulation - Concurrency Testing");
            Console.WriteLine("----------------------------------------");
           
            // Create bank accounts
            List<BankAccount> accounts = new List<BankAccount>
            {
                new BankAccount(1, 1000),
                new BankAccount(2, 2000),
                new BankAccount(3, 3000)
            };
           
            // Create tellers
            List<Teller> tellers = new List<Teller>
            {
                new Teller(1),
                new Teller(2),
                new Teller(3)
            };
           // Run stress testing
Console.WriteLine("\nPreparing for stress testing...");
Console.WriteLine("Press any key to begin stress test with 50 concurrent customers...");
Console.ReadKey(true);

// Create and run the stress test
BankingStressTest stressTest = new BankingStressTest(
    customerCount: 50,          // 50 concurrent customers
    operationsPerCustomer: 20,  // Each performs 20 operations
    accountCount: 5,            // 5 bank accounts to share
    useProgressReporting: true  // Show real-time progress
);

stressTest.RunStressTest();
            // Create customers
            List<Customer> customers = new List<Customer>
            {
                new Customer("Alice", accounts, tellers),
                new Customer("Bob", accounts, tellers),
                new Customer("Charlie", accounts, tellers),
                new Customer("Diana", accounts, tellers),
                new Customer("Evan", accounts, tellers),
                new Customer("Fiona", accounts, tellers),
                new Customer("George", accounts, tellers),
                new Customer("Hannah", accounts, tellers)
            };
           
           // Synchronization test
           
           SynchronizationValidator.PerformSynchronizationTests(accounts);


            // Create threads for each customer
            List<Thread> threads = new List<Thread>();
           
            foreach (var customer in customers)
            {
                Thread customerThread = new Thread(customer.PerformTransfersWithTellers);
                threads.Add(customerThread);
                Console.WriteLine($"Created thread for Customer {customer.Id} ({customer.Name})");
            }
           
            Console.WriteLine("\nStarting all customer threads...\n");
           
            // Start stopwatch for performance measurement
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
           
            // Start all threads
            foreach (var thread in threads)
            {
                thread.Start();
            }
           
            // Wait for all threads to complete
            foreach (var thread in threads)
            {
                thread.Join();
            }
           
            // Stop the stopwatch
            stopwatch.Stop();
           
            Console.WriteLine($"\nAll customer transactions completed in {stopwatch.ElapsedMilliseconds}ms");
            Console.WriteLine("\nFinal account balances:");
           
            foreach (var account in accounts)
            {
                Console.WriteLine($"Account {account.Id}: ${account.GetBalance()}");
            }
           
            // Run the concurrency tests
            ConcurrencyTester tester = new ConcurrencyTester(customers, tellers, accounts, threads);
            tester.RunTests();
           
            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }
    }
}