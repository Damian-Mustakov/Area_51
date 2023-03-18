using Area_51.Enums;

namespace Area_51
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var tasks = new List<Task>();
            var random = new Random();
            var elevator = new Elevator();

            var firstAgent = new Agent("Smith", elevator, random, AccessLevelEnum.Confidential);
            var secondAgent = new Agent("Jones", elevator, random, AccessLevelEnum.Confidential);
            var thirdAgent = new Agent("Lazar", elevator, random, AccessLevelEnum.Confidential);

            tasks.Add(Task.Run(elevator.Start));

            tasks.Add(Task.Run(firstAgent.GoAroundTheBase));
            tasks.Add(Task.Run(secondAgent.GoAroundTheBase));
            tasks.Add(Task.Run(thirdAgent.GoAroundTheBase));

            // Stop the elevator after 60 seconds
            tasks.Add(Task.Delay(60_000).ContinueWith(t => elevator.Stop()));
            //waiting for all the tasks to complete
            Task.WhenAll(tasks).Wait();

            Console.WriteLine("Press enter to quit");
            Console.ReadLine();
        }
    }
}