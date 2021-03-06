﻿using System;
using System.Collections.Generic;
using System.Linq;
using AVThesis.Datastructures;
using AVThesis.SabberStone.Bots;
using AVThesis.SabberStone.Strategies;
using AVThesis.Search;
using AVThesis.Search.Tree;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using State = SabberStoneCore.Enums.State;

/// <summary>
/// Copyright © 2019 Anton Valkenberg
/// Written by BSc. A.J.J. Valkenberg, used in his Master Thesis on Artificial Intelligence.
/// In parts inspired by a code framework written by MSc. G.-J. Roelofs, MSc. T. Aliyev and MSc. D. de Rydt.
/// </summary>
namespace AVThesis.SabberStone {

    /// <summary>
    /// Represents a single search in SabberStone and fulfils some administrative tasks.
    /// </summary>
    public class SabberStoneSearch {

        #region Fields

        private readonly bool _debug;

        #endregion

        #region Properties

        /// <summary>
        /// The player in a game of SabberStone that this search is for.
        /// </summary>
        public Controller Player { get; set; }

        /// <summary>
        /// Statistics on PlayerTasks indexed by the task's hashcode.
        /// </summary>
        public Dictionary<int, PlayerTaskStatistics> TaskStatistics { get; set; }

        /// <summary>
        /// The maximum depth reached by the current search.
        /// Note: only work for searches of Type <see cref="TreeSearch{D,P,A,S,Sol}"/>.
        /// </summary>
        public int CurrentMaxDepth { get; set; }

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new instance of this searcher.
        /// </summary>
        /// <param name="player">The player in SabberStone to search for.</param>
        /// <param name="debugToConsole">[Optional] Whether or not to write debug information to the console. Default value is false.</param>
        public SabberStoneSearch(Controller player, bool debugToConsole = false) {
            Player = player;
            _debug = debugToConsole;
            TaskStatistics = new Dictionary<int, PlayerTaskStatistics>();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Runs a single search.
        /// Note: this method is called by the <see cref="EnsembleStrategySabberStone"/>.
        /// </summary>
        /// <param name="context">The context of the search.</param>
        /// <returns>SabberStoneAction that is the solution to the search.</returns>
        public SabberStoneAction Search(SearchContext<List<SabberStoneAction>, SabberStoneState, SabberStoneAction, object, SabberStoneAction> context) {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            if (_debug) Console.WriteLine();

            // Execute the search
            context.Execute();

            // Check if the search was successful
            if (context.Status != SearchContext<List<SabberStoneAction>, SabberStoneState, SabberStoneAction, object, SabberStoneAction>.SearchStatus.Success) {
                throw new SearchException($"Search did not conclude correctly. Current Status {context.Status}");
            }

            var solution = context.Solution;

            // Check if the search has a MASTPlayoutBot as PlayoutStrategy so that those task-values can be used
            var playoutStrategyProperty = context.Search.GetType().GetProperty("PlayoutStrategy");
            if (playoutStrategyProperty != null) {
                var playoutStrategy = playoutStrategyProperty.GetMethod.Invoke(context.Search, null);
                if (playoutStrategy is PlayoutStrategySabberStone playout) {
                    if (playout.Bots.ContainsKey(context.Source.CurrentPlayer())) {
                        var contextBot = playout.Bots[context.Source.CurrentPlayer()];
                        if (contextBot is MASTPlayoutBot playoutBot) {
                            // Use the MAST statistics as a baseline
                            TaskStatistics = new Dictionary<int, PlayerTaskStatistics>(playoutBot.MASTTable);
                        }
                    }
                }
            }

            // Check if the search has a SolutionStrategy so that task-values can be saved
            if (context.Search is TreeSearch<List<SabberStoneAction>, SabberStoneState, SabberStoneAction, object, SabberStoneAction> search) {
                // Retrieve the task values from the solution strategy and process them into our property
                var solutionStrategy = (SolutionStrategySabberStone)search.SolutionStrategy;
                foreach (var tuple in solutionStrategy.TaskValues) {
                    var taskHash = tuple.Item1.GetHashCode();
                    if (!TaskStatistics.ContainsKey(taskHash)) TaskStatistics.Add(taskHash, new PlayerTaskStatistics(tuple.Item1, tuple.Item2));
                    else TaskStatistics[taskHash].AddValue(tuple.Item2);
                }

                // Record the search's maximum depth
                CurrentMaxDepth = search.MaxDepth;

                // Make sure to clear the values for the next search
                solutionStrategy.ClearTaskValues();
            }

            if (_debug) Console.WriteLine($"Searcher returned with solution: {solution}");
            if (_debug) Console.WriteLine($"Calculation time was: {timer.ElapsedMilliseconds} ms.");
            if (_debug) Console.WriteLine();
            return solution;
        }

        /// <summary>
        /// Determines the best tasks for the game state based on the provided statistics and creates a <see cref="SabberStoneAction"/> from them.
        /// </summary>
        /// <param name="state">The game state to create the best action for.</param>
        /// <returns><see cref="SabberStoneAction"/> created from the best individual tasks available in the provided state.</returns>
        public SabberStoneAction DetermineBestTasks(SabberStoneState state) {
            // Clone game so that we can process the selected tasks and get an updated options list.
            var clonedGame = state.Game.Clone();

            // We have to determine which tasks are the best to execute in this state, based on the provided values of the MCTS search.
            // So we'll check the statistics table for the highest value among tasks that are currently available in the state.
            // This continues until the end-turn task is selected.
            var action = new SabberStoneAction();
            while (!action.IsComplete() && clonedGame.State != State.COMPLETE) {
                // Get the available options in this state and find which tasks we have statistics on, but ignore the END-TURN task for now
                var availableTasks = clonedGame.CurrentPlayer.Options().Where(i => i.PlayerTaskType != PlayerTaskType.END_TURN).Select(i => ((SabberStonePlayerTask)i).GetHashCode());
                var stats = TaskStatistics.Where(i => availableTasks.Contains(i.Key)).ToList();
                var bestTask = stats.OrderByDescending(i => i.Value.AverageValue()).FirstOrDefault();

                // If we can't find any task, stop.
                if (bestTask.IsDefault()) {
                    // End the turn
                    action.AddTask((SabberStonePlayerTask)EndTurnTask.Any(clonedGame.CurrentPlayer));
                    break;
                }

                // Handle the possibility of tasks with tied average value.
                var bestValue = bestTask.Value.AverageValue();
                var tiedTasks = stats.Where(i => Math.Abs(i.Value.AverageValue() - bestValue) < Constants.DOUBLE_EQUALITY_TOLERANCE);
                var orderedTies = tiedTasks.OrderByDescending(i => i.Value.Visits);
                bestTask = orderedTies.First();

                // If we found a task, add it to the Action and process it to progress the game.
                var task = bestTask.Value.Task;
                action.AddTask(task);
                clonedGame.Process(task.Task);
            }

            // Return the created action consisting of the best action available at each point.
            return action;
        }

        /// <summary>
        /// Resets the statistics on the individual tasks.
        /// </summary>
        public void ResetTaskStatistics() {
            TaskStatistics = new Dictionary<int, PlayerTaskStatistics>();
        }

        /// <summary>
        /// Creates a SabberStoneAction from a collection of possible solutions by voting for separate tasks.
        /// </summary>
        /// <param name="solutions">The available solutions.</param>
        /// <param name="state">The game state.</param>
        /// <returns>SabberStoneAction.</returns>
        public SabberStoneAction VoteForSolution(List<SabberStoneAction> solutions, SabberStoneState state) {
            // Clone game so that we can process the selected tasks and get an updated options list.
            var clonedGame = state.Game.Clone();
            var action = new SabberStoneAction();

            // Have all solutions vote on tasks
            var taskVotes = new Dictionary<int, int>();
            foreach (var solution in solutions) {
                foreach (var task in solution.Tasks) {
                    var taskHash = task.GetHashCode();
                    if (!taskVotes.ContainsKey(taskHash)) taskVotes.Add(taskHash, 0);
                    taskVotes[taskHash]++;
                }
            }

            // Keep selecting tasks until the action is complete or the game has ended
            while (!action.IsComplete() && clonedGame.State != State.COMPLETE) {
                // Make a dictionary of available tasks, indexed by their hashcode, but ignore the END-TURN task for now
                var availableTasks = clonedGame.CurrentPlayer.Options().Where(i => i.PlayerTaskType != PlayerTaskType.END_TURN).Select(i => (SabberStonePlayerTask)i).ToList();

                // Pick the one with most votes
                var votedOnTasks = availableTasks.Where(i => taskVotes.ContainsKey(i.GetHashCode())).ToList();
                var mostVoted = votedOnTasks.OrderByDescending(i => taskVotes[i.GetHashCode()]).FirstOrDefault();
                
                // If this is null, it means none of the tasks that are available appear in any of the solutions
                if (mostVoted == null) {
                    // End the turn
                    action.AddTask((SabberStonePlayerTask)EndTurnTask.Any(clonedGame.CurrentPlayer));
                    break;
                }

                // Find any tasks tied for most votes
                var mostVotes = taskVotes[mostVoted.GetHashCode()];
                var ties = votedOnTasks.Where(i => taskVotes[i.GetHashCode()] == mostVotes);

                // Add one of the tasks with the most votes to the action
                //TODO Ties during voting can be handled differently than random, but handling ties based on visit count would require extra information from the separate searches' solutions.
                var chosenTask = ties.RandomElementOrDefault();
                action.AddTask(chosenTask);

                // Process the task so we have an updated options list next iteration
                clonedGame.Process(chosenTask.Task);
            }

            return action;
        }

        #endregion

    }

}
