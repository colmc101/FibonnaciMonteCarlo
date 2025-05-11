// **** A Monte Carlo Simulation of a Scrum team ****
//
// The team estimates user stories, giving each story a fibonacci number of 'story points', up to a maximum acceptable story size.
//
// The team accepts no more stories in a 'sprint' than their average 'story point velocity' from recent sprints 
// 'velocity' is the total points on stories that were fully completed during the sprint.
//
// We repeat the process many times, to see how accurately velocity can be predicted - in principle - when using Fibonnaci estimation
// 
//ASSUMPTIONS:
//  The size of any story is a random pick from the available Fib numbers (uniform distribution of points per story).
//  The team completes randomly more or less of the total work than predicted. 
//      - on average they complete exactly all the stories
//      - they can be up to 50% out in their prediction (normally distributed around the average of 100%, standard deviation of 20%)
//      - the starting (first guess) velocity is 50 points
//        (imagining 5 developers working 10-day sprints, where a developer completes work that is notionally equivalent to 1SP/day)
//
using System.Collections;
using System.Collections.Generic;
using MathNet.Numerics.Distributions;

var InitialVelocity = 50;
var FibonnaciSequence = new int[] { 1, 2, 3, 5, 8, 13, 21, 34 };
var SprintsPerSimulation = 50; // roughly, the team follows the practice for 2 years
var IterationsOfSimulation = 1000;

Console.WriteLine("Fibonacci goes to Monte Carlo!");

Console.WriteLine(String.Format("Simulating a Sequence of {0} sprints, {1} times.", SprintsPerSimulation, IterationsOfSimulation));
Console.WriteLine("(params: adjust sprint capacity to equal average of last 10 sprints, but never less than 25 points");
Console.WriteLine("-------------------------------------------------------------------------");
Console.WriteLine();

for (var i = FibonnaciSequence.Length; i>=1; i--)
{
   var result = new Simulator(InitialVelocity, SprintsPerSimulation, IterationsOfSimulation).Run(FibonnaciSequence[i-1]);
   Console.WriteLine(String.Format("MAX STORY SIZE: {0} ", FibonnaciSequence[i-1]));
   Console.WriteLine("---------------------Results----------------------------------------");
   Console.WriteLine(String.Format("VELOCITY  - First Sprint {0}, Last Sprint {1}, Average {2}", result.meanInitialVelocity, result.meanFinalVelocity, result.meanVelocity));
   Console.WriteLine(String.Format("%AGE ERROR IN PLANNING  - First Sprint {0}, Last Sprint {1}, Average {2}", result.meanInitialAccuracy, result.meanFinalAccuracy, result.meanAccuracy));
   Console.WriteLine();
   Console.WriteLine();
}

Console.WriteLine("Done. Press any key to exit");
Console.ReadLine();

public class Simulator
{
    public int InitialVelocity{  get; set; }
    public int SprintsCount{ get; set; }
    public int IterationsCount {  get; set; }

    static Random random = new Random();

    public Simulator(int initialVelocity, int sprints, int iterations)
    {
        InitialVelocity = initialVelocity;
        IterationsCount = iterations;
        SprintsCount = sprints;
    }
    /// <summary>
    /// Simulates a sprint, containing stories. Each story
    /// is 'worked on' at random, until its SPs are decremented to 0.
    /// </summary>
    /// <param name="maxSPs">maximum number of story points allowed per story</param>
    /// <returns></returns>
    public SimulationResult Run(int maxSPs)
    {
        var cycles = new List<CycleOfSprints>();
        for(var i = 0; i < IterationsCount; i++)
        {
            cycles.Add(SprintCycle(maxSPs));
        }

        return CalculateFullStats(cycles);
    }
    public SimulationResult CalculateFullStats(List<CycleOfSprints> cycles)
    {
        var result = new SimulationResult();

        result.meanFinalVelocity = Convert.ToInt32(cycles.Select(c => c.finalVelocity).Average());
        result.meanInitialVelocity = Convert.ToInt32(cycles.Select(c => c.initialVelocity).Average());
        result.meanVelocity = Convert.ToInt32(cycles.Select(c => c.meanVelocity).Average());

        result.meanInitialAccuracy = Convert.ToInt32(cycles.Select(c => c.initialAccuracy).Average());
        result.meanFinalAccuracy = Convert.ToInt32(cycles.Select(c => c.finalAccuracy).Average());
        result.meanAccuracy = Convert.ToInt32(cycles.Select(c => c.meanAccuracy).Average());

        return result;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="maxSPs"></param>
    /// <returns></returns>
    public CycleOfSprints SprintCycle(int maxSPs)
    {
        var sprints = new List<Sprint>();
        var velocities = new List<int>();

        int nextPredictedVelocity;

        for(var i = 0; i<SprintsCount; i++)
        { 
        
            if (velocities.Count > 0)
            {
                var recentVelocity = MeanRecentVelocity(velocities);
                nextPredictedVelocity = Math.Max(recentVelocity, 25);
            }
            else
            {
                nextPredictedVelocity = InitialVelocity;
            }

            sprints.Add( ConductSprint(maxSPs, nextPredictedVelocity) );
            velocities.Add(sprints.Last().pointsCompleted);
        }

        return CalculateCycleStats(sprints);
    }

    public Sprint ConductSprint(int maxSPs, int predictedVelocity)
    {
        var FibonnaciSequence = new int[] { 1, 2, 3, 5, 8, 13, 21, 34 };
        var sprint = new Sprint();
        sprint.pointsPredicted = predictedVelocity;

        var availableStorySizes = FibonnaciSequence.Where(f => f<=maxSPs).ToList();
        var stories = PlanSprint(availableStorySizes, predictedVelocity);
        var originalStories = new List<int>(stories); // keep a clone for later
        var remainingCapacity = VaryCapacity(predictedVelocity);

        while (remainingCapacity > 0)
        {
            var indexesOfIncompleteStories = new List<int>();
            for ( var i = 0; i < stories.Count; i++)
            {
                if(stories[i] > 0) indexesOfIncompleteStories.Add(i);
            }

            if (indexesOfIncompleteStories.Count == 0) break;

            //pick an uncopleted story at random, decrment its points
            var r = random.Next(indexesOfIncompleteStories.Count - 1);
            var ix = indexesOfIncompleteStories[r];

            if (stories[ix] > 0)
            {
                stories[ix] = stories[ix] - 1;
                remainingCapacity--;
            }
        }

        var completedPoints = 0;
        for (int i = 0; i < stories.Count; i++)
        {
            if(stories[i] == 0)
            {
                completedPoints = completedPoints + originalStories[i];
            }
        }
        sprint.pointsCompleted = completedPoints;
        return sprint;
    }

    public List<int> PlanSprint(List<int> sizes, int prediction)
    {
        var stories = new List<int>();
        var sprintIsFull = false;
        var minSize = sizes.Min();

        while (!sprintIsFull)
        {
            var sprintSize = stories.Sum();
            var candidate = sizes[random.Next(sizes.Count -1)];
            if (sprintSize + candidate <= prediction)
            {
                stories.Add(candidate);
                sprintIsFull = (sprintSize + candidate >= prediction);
            }          
        }

        return stories;
    }

    public int VaryCapacity(int predictedVelocity) {
        var stdDev = predictedVelocity * 0.2; // standard deviation of +/- 20% 
        var sample = Convert.ToInt32(Normal.Sample(random, predictedVelocity, stdDev));
        return sample;
    }
 
    /// <summary>
    /// returns the 10 most recent velocities, or all if there have been fewer than 10 sprints so far
    /// </summary>
    /// <param name="velocities"></param>
    /// <returns></returns>
    public int MeanRecentVelocity(List<int> velocities)
    {
        var TenMostRecent = velocities.Skip(Math.Max(0, velocities.Count() - 10));
        return Convert.ToInt32(TenMostRecent.Average());
    }

    public CycleOfSprints CalculateCycleStats(List<Sprint> sprints)
    {
        var cycle = new CycleOfSprints();
        cycle.initialVelocity = sprints.First().pointsCompleted;
        cycle.finalVelocity = sprints.Last().pointsCompleted;
        cycle.meanVelocity = Convert.ToInt32(sprints.Select(s => s.pointsCompleted).Average());

        var accuracies = new List<double>();
        accuracies = sprints.Select(x => 100.00 * (double)(x.pointsPredicted - x.pointsCompleted) / (double)(x.pointsPredicted)).ToList();

        cycle.initialAccuracy = Convert.ToInt32(accuracies.First());
        cycle.finalAccuracy = Convert.ToInt32(accuracies.Last());
        cycle.meanAccuracy = Convert.ToInt32(accuracies.Average());

        return cycle;

    }
}

public class SimulationResult
{
    public int maxSPs {  get; set; }
    public int meanInitialVelocity {  get; set; }
    public int meanFinalVelocity { get; set; }
    public int meanVelocity { get; set; }
    public int meanAccuracy { get; set; }
    public int meanInitialAccuracy { get; set; }
    public int meanFinalAccuracy {  get; set; }

}
/// <summary>
/// tracks the predicted v. completed points per sprint, and the %age accuracy.
/// </summary>
public class Sprint { 
    public int pointsPredicted {  get; set; }
    public int pointsCompleted { get; set; }
}

/// <summary>
/// Simulates a team adjusting running sprints over 2 years.
/// They predict the number of points they can complete per sprint, based on mean velocity of the previous 10 sprints
/// </summary>
public class CycleOfSprints
{
    public int initialVelocity { get; set; }
    public int finalVelocity { get; set; }
    public int meanVelocity { get; set; }
    public int initialAccuracy { get; set; }
    public int meanAccuracy { get; set; }
    public int finalAccuracy { get; set; }
}
