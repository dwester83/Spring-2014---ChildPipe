using System;
using System.Collections.Generic; // For the linked list.
using System.Linq; // For generics in a list.
using System.IO.Pipes; // Pipes are contained in here.
using System.IO; // This is needed for the StreamReader.
using System.Threading; // Allows me to sleep this thread.

namespace PipesClient
{
    unsafe class Program
    {
        // Getting my constants
        private static readonly int LIST_SIZE = 50000;
        private static readonly int WORK_LOAD = 50; // How much work is going to be done.
        private static readonly int SLEEP_TIME = 20; // This is how long the process will sleep for.

        static void Main(string[] args)
        {
            // This prevents the program from running, if the StartInfo.Args hasn't been set
            if (args.Length > 0)
            {
                String name = "";
                Boolean debug = false;
                Random random = new Random();
                int randomNumber;
                //int workDone = 0;
                AtomicInt workDone = new AtomicInt(0);
                Boolean endProgramBool = false;

                LinkedList<int> list = new LinkedList<int>();

                // Creation of the list.
                for (int i = 0; i < LIST_SIZE; i++)
                {
                    LinkedListNode<int> node = new LinkedListNode<int>(i);
                    list.AddLast(node);
                }

                // This is to show that the values are in the list, this is just for myself to know the list is correct.
                /*for (int i = 0; i < 50000; i++)
                {
                    Console.WriteLine(list.ElementAt<int>(i));
                }*/

                // Client name, only seen in the testing mode.
                name = "[CHILD " + args[2] + "] ";

                // Seeing if the process is in debug mode.
                if (args[3].Equals("true"))
                {
                    debug = true;
                }

                // Creating the pipeMonitor to watch the pipe activity.
                PipeMonitor pipeMonitor = new PipeMonitor(
                    args[0],
                    args[1],
                    ref workDone, // ref is for passing by reference for objects
                    &endProgramBool, // & is the reference for primatives
                    debug,
                    name);

                // Create the thread object, passing in the PipeMonitor method
                // via a ThreadStart delegate. This does not start the thread.
                Thread monitorThread = new Thread(new ThreadStart(pipeMonitor.Monitor));

                // Starts the thread.
                monitorThread.Start();

                // Debug, will print out what handler number was given for the in and out pipe.
                if (debug)
                {
                    Console.WriteLine(name + "Pipe Direction Out Handler: {0}", args[0]);
                    Console.WriteLine(name + "Pipe Direction In Handler:  {0}", args[1]);
                }

                // This loop will run into the boolean is changed to true.
                while (!endProgramBool)
                {
                    // Making the process doing a bunch of work.
                    for (int i = 0; i < WORK_LOAD; i++)
                    {
                        // Random number to look for and see if it's in the list.
                        randomNumber = random.Next(0, LIST_SIZE);
                        list.Contains<int>(randomNumber);
                    }

                    // Locking the object so nothing else can accesses it until the lock is removed.
                    lock (workDone)
                    {
                        // Increasing the work done.
                        workDone.Increment();
                    }

                    // Putting the thread to sleep until it starts the process over.
                    Thread.Sleep(SLEEP_TIME);
                }

                // Closing the thread that watches the pipes.
                monitorThread.Join();

                // Printing to the console the program is closed.
                if (debug)
                    Console.WriteLine(name + "The parent process has sent Stop, closing program.");
            }

            // If no arguments were given the program will not run.
            if (args.Length == 0)
                Console.WriteLine("No arguments were given, closing program.");
        }
    }

    public unsafe class PipeMonitor
    {
        AnonymousPipeClientStream pipeClientOut;
        AnonymousPipeClientStream pipeClientIn;
        AtomicInt workDone;
        Boolean* endProgramBool;
        Boolean debug;
        String name;

        // Default Constructor, will set up the pipe watching thread.
        public PipeMonitor(String pipeOut, String pipeIn, ref AtomicInt workDone, Boolean* endProgramBool, Boolean debug, String name)
        {
            pipeClientOut = new AnonymousPipeClientStream(PipeDirection.Out, pipeOut);
            pipeClientIn = new AnonymousPipeClientStream(PipeDirection.In, pipeIn);
            this.workDone = workDone;
            this.endProgramBool = endProgramBool;
            this.debug = debug;
            this.name = name;
        }

        // This method that will be called when the thread is started
        public void Monitor()
        {
            String input;
            // Stop, Update

            try
            {
                // Stream reader to read what's sent by the parent process.
                using (StreamWriter sw = new StreamWriter(pipeClientOut))
                {
                    sw.AutoFlush = true;

                    using (StreamReader sr = new StreamReader(pipeClientIn))
                    {
                        while (!*endProgramBool)
                        {
                            // Blocked, this thread is waiting until it reads
                            // something that comes into the pipe.
                            input = sr.ReadLine();

                            if (input != "")
                            {
                                // If the value read is Update, will get the work done and update reset the value to 0.
                                if (input.Equals("Update"))
                                {
                                    // Lock is the same as synchronize, requires an object to be locked.
                                    lock (workDone)
                                    {
                                        // Will write the work that's been done, and set the number to 0.
                                        sw.WriteLine(workDone.GetAndSet(0));
                                    }

                                    // If debug mode is on, will display that it transmitted.
                                    if (debug)
                                        Console.WriteLine(name + "Transmitted.");

                                    // Will wait for the pipe to be read on the other end before going back to waiting on a message.
                                    pipeClientOut.WaitForPipeDrain();
                                }
                                    // If the message is Stop, will change the end Boolean to true.
                                else if (input.Equals("Stop"))
                                    *endProgramBool = true;
                            }
                        } // End while loop.
                    } // End of Stream Reader.
                } // End of Stream Writer.
            }
            catch (IOException e)
            {
                Console.WriteLine(name + "Error: {0}", e.Message);
            }

        }
    }


    // Since lock requires an object, I made my own object to be my own Atomic Integer.
    public class AtomicInt
    {
        volatile int atomic = 0;

        // Default constructor, sets the int to the number that's passed.
        public AtomicInt(int input)
        {
            atomic = input;
        }
        
        // Gets the number and sets it to the int that's being passed.
        public int GetAndSet(int set)
        {
            int temp = atomic;
            atomic = set;
            return temp;
        }

        // Increases the work done by one.
        public void Increment()
        {
            atomic++;
        }
    }


}
