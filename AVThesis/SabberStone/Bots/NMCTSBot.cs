﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using AVThesis.Enums;
using AVThesis.Game;
using AVThesis.SabberStone.Strategies;
using AVThesis.Search;
using AVThesis.Search.Tree.NMC;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

/// <summary>
/// Copyright © 2019 Anton Valkenberg
/// Written by BSc. A.J.J. Valkenberg, used in his Master Thesis on Artificial Intelligence.
/// In parts inspired by a code framework written by MSc. G.-J. Roelofs, MSc. T. Aliyev and MSc. D. de Rydt.
/// </summary>
namespace AVThesis.SabberStone.Bots {

    /// <summary>
    /// A bot that plays Hearthstone using Monte Carlo Tree Search with Naïve Sampling to find its best move.
    /// </summary>
    public class NMCTSBot : ISabberStoneBot {

        #region Inner Classes

        /// <summary>
        /// Handles the sampling of states during the NaïveSampling process.
        /// </summary>
        private class SabberStoneNMCSamplingStrategy : ISamplingStrategy<SabberStoneState, SabberStoneAction> {

            #region Properties

            /// <summary>
            /// The bot that creates the sample actions.
            /// </summary>
            private RandomBot NaïveBot { get; }

            #endregion

            #region Constructors

            /// <summary>
            /// Constructs a new instance.
            /// </summary>
            /// <param name="naïveBot">The bot to use when creating samples.</param>
            public SabberStoneNMCSamplingStrategy(RandomBot naïveBot) {
                NaïveBot = naïveBot;
            }

            #endregion

            #region Public Methods

            /// <inheritdoc />
            public SabberStoneAction Sample(SabberStoneState state) {
                return NaïveBot.CreateRandomAction(state);
            }

            #endregion

        }

        #endregion

        #region Constants

        private const string BOT_NAME = "NMCTSBot";
        private readonly bool _debug;

        #endregion

        #region Properties

        /// <summary>
        /// The player this bot is representing in a game of SabberStone.
        /// </summary>
        public Controller Player { get; set; }

        /// <summary>
        /// The bot that is used during the playouts.
        /// </summary>
        public IPlayoutBot MyPlayoutBot { get; set; }

        /// <summary>
        /// The bot that is used for the opponent's playouts.
        /// </summary>
        public IPlayoutBot OpponentPlayoutBot { get; set; }

        /// <summary>
        /// The type of playout bot to be used during playouts.
        /// </summary>
        public PlayoutBotType PlayoutBotType { get; set; }

        /// <summary>
        /// A bot used during the NaïveSampling process.
        /// </summary>
        public RandomBot RandomSamplingBot { get; set; }

        /// <summary>
        /// The strategy used to determine if a playout has reached its goal state.
        /// </summary>
        public IGoalStrategy<List<SabberStoneAction>, SabberStoneState, SabberStoneAction, object, SabberStoneAction> Goal { get; set; }

        /// <summary>
        /// The game specific logic required for searching through SabberStoneStates and SabberStoneActions
        /// </summary>
        public IGameLogic<List<SabberStoneAction>, SabberStoneState, SabberStoneAction, object, SabberStoneAction, SabberStoneAction> GameLogic { get; set; }

        /// <summary>
        /// The strategy used to play out a game in simulation.
        /// </summary>
        public PlayoutStrategySabberStone Playout { get; set; }

        /// <summary>
        /// The Naïve Monte Carlo Tree Search builder that creates the search-setup ready to use.
        /// </summary>
        public NMCTSBuilder<List<SabberStoneAction>, SabberStoneState, SabberStoneAction, object, SabberStoneAction> Builder { get; set; }

        /// <summary>
        /// Whether or not this bot is allowed perfect information about the game state (i.e. no obfuscation and therefore no determinisation).
        /// </summary>
        public bool PerfectInformation { get; set; }

        /// <summary>
        /// The size of the ensemble the search should use.
        /// </summary>
        public int EnsembleSize { get; set; }

        /// <summary>
        /// The ensemble strategy to use.
        /// </summary>
        public EnsembleStrategySabberStone Ensemble { get; set; }

        /// <summary>
        /// The solutions received from the ensemble.
        /// </summary>
        public List<SabberStoneAction> EnsembleSolutions { get; set; }

        /// <summary>
        /// Does the administrative tasks around searching.
        /// </summary>
        public SabberStoneSearch Searcher { get; set; }

        /// <summary>
        /// The type of selection strategy used by the MAST playout.
        /// </summary>
        public MASTPlayoutBot.SelectionType MASTSelectionType { get; set; }

        /// <summary>
        /// The cutoff for amount of turns simulated during playout.
        /// </summary>
        public int PlayoutTurnCutoff { get; set; }

        /// <summary>
        /// The budget for the amount of iterations NMCTS can use.
        /// </summary>
        public int Iterations { get; set; }

        /// <summary>
        /// The total amount of iterations spent during the calculation of the latest solution.
        /// </summary>
        public long IterationsSpent { get; set; }

        /// <summary>
        /// The budget for the amount of milliseconds MCTS can spend on searching.
        /// </summary>
        public long Time { get; set; }

        /// <summary>
        /// The type of budget that this bot will use.
        /// </summary>
        public BudgetType BudgetType { get; set; }

        /// <summary>
        /// The exploration-threshold for the e-greedy global policy.
        /// </summary>
        public double GlobalPolicy { get; set; }

        /// <summary>
        /// The exploration-threshold for the e-greedy local policy.
        /// </summary>
        public double LocalPolicy { get; set; }

        #endregion

        #region Constructors

        /// <summary>
        /// Constructs a new instance of NMCTSBot with a <see cref="Controller"/> representing the player.
        /// </summary>
        /// <param name="player">The player.</param>
        /// <param name="allowPerfectInformation">[Optional] Whether or not this bot is allowed perfect information about the game state (i.e. no obfuscation and therefore no determinisation). Default value is false.</param>
        /// <param name="ensembleSize">[Optional] The size of the ensemble to use. Default value is 1.</param>
        /// <param name="playoutBotType">[Optional] The type of playout bot to be used during playouts. Default value is <see cref="PlayoutBotType.MAST"/>.</param>
        /// <param name="mastSelectionType">[Optional] The type of selection strategy used by the MAST playout. Default value is <see cref="MASTPlayoutBot.SelectionType.EGreedy"/>.</param>
        /// <param name="budgetType">[Optional] The type of budget that this bot will use. Default value is <see cref="BudgetType.Iterations"/>.</param>
        /// <param name="iterations">[Optional] The budget for the amount of iterations NMCTS can use. Default value is <see cref="Constants.DEFAULT_COMPUTATION_ITERATION_BUDGET"/>.</param>
        /// <param name="time">[Optional] The budget for the amount of milliseconds NMCTS can spend on searching. Default value is <see cref="Constants.DEFAULT_COMPUTATION_TIME_BUDGET"/>.</param>
        /// <param name="playoutTurnCutoff">[Optional] The amount of turns after which to stop a simulation. Default value is <see cref="Constants.DEFAULT_PLAYOUT_TURN_CUTOFF"/>.</param>
        /// <param name="globalPolicy">[Optional] The exploration-threshold for the e-greedy global policy. Default value is <see cref="Constants.DEFAULT_NMCTS_GLOBAL_POLICY"/>.</param>
        /// <param name="localPolicy">[Optional] The exploration-threshold for the e-greedy local policy. Default value is <see cref="Constants.DEFAULT_NMCTS_LOCAL_POLICY"/>.</param>
        /// <param name="useHeuristicEvaluation">[Optional] Whether or not to use the HeuristicBot's evaluation function. Default value is false.</param>
        /// <param name="debugInfoToConsole">[Optional] Whether or not to write debug information to the console. Default value is false.</param>
        public NMCTSBot(Controller player,
            bool allowPerfectInformation = false,
            int ensembleSize = 1,
            PlayoutBotType playoutBotType = PlayoutBotType.MAST,
            MASTPlayoutBot.SelectionType mastSelectionType = MASTPlayoutBot.SelectionType.EGreedy,
            BudgetType budgetType = BudgetType.Iterations,
            int iterations = Constants.DEFAULT_COMPUTATION_ITERATION_BUDGET,
            long time = Constants.DEFAULT_COMPUTATION_TIME_BUDGET,
            int playoutTurnCutoff = Constants.DEFAULT_PLAYOUT_TURN_CUTOFF,
            double globalPolicy = Constants.DEFAULT_NMCTS_GLOBAL_POLICY,
            double localPolicy = Constants.DEFAULT_NMCTS_LOCAL_POLICY,
            bool useHeuristicEvaluation = false,
            bool debugInfoToConsole = false)
            : this(allowPerfectInformation, ensembleSize, playoutBotType, mastSelectionType, budgetType, iterations, time, playoutTurnCutoff, globalPolicy, localPolicy, useHeuristicEvaluation, debugInfoToConsole) {
            SetController(player);
        }

        /// <summary>
        /// Constructs a new instance of NMCTSBot with default strategies.
        /// </summary>
        /// <param name="allowPerfectInformation">[Optional] Whether or not this bot is allowed perfect information about the game state (i.e. no obfuscation and therefore no determinisation). Default value is false.</param>
        /// <param name="ensembleSize">[Optional] The size of the ensemble to use. Default value is 1.</param>
        /// <param name="playoutBotType">[Optional] The type of playout bot to be used during playouts. Default value is <see cref="PlayoutBotType.MAST"/>.</param>
        /// <param name="mastSelectionType">[Optional] The type of selection strategy used by the MAST playout. Default value is <see cref="MASTPlayoutBot.SelectionType.EGreedy"/>.</param>
        /// <param name="budgetType">[Optional] The type of budget that this bot will use. Default value is <see cref="BudgetType.Iterations"/>.</param>
        /// <param name="iterations">[Optional] The budget for the amount of iterations NMCTS can use. Default value is <see cref="Constants.DEFAULT_COMPUTATION_ITERATION_BUDGET"/>.</param>
        /// <param name="time">[Optional] The budget for the amount of milliseconds NMCTS can spend on searching. Default value is <see cref="Constants.DEFAULT_COMPUTATION_TIME_BUDGET"/>.</param>
        /// <param name="playoutTurnCutoff">[Optional] The amount of turns after which to stop a simulation. Default value is <see cref="Constants.DEFAULT_PLAYOUT_TURN_CUTOFF"/>.</param>
        /// <param name="globalPolicy">[Optional] The exploration-threshold for the e-greedy global policy. Default value is <see cref="Constants.DEFAULT_NMCTS_GLOBAL_POLICY"/>.</param>
        /// <param name="localPolicy">[Optional] The exploration-threshold for the e-greedy local policy. Default value is <see cref="Constants.DEFAULT_NMCTS_LOCAL_POLICY"/>.</param>
        /// <param name="useHeuristicEvaluation">[Optional] Whether or not to use the HeuristicBot's evaluation function. Default value is false.</param>
        /// <param name="debugInfoToConsole">[Optional] Whether or not to write debug information to the console. Default value is false.</param>
        public NMCTSBot(bool allowPerfectInformation = false,
            int ensembleSize = 1,
            PlayoutBotType playoutBotType = PlayoutBotType.MAST,
            MASTPlayoutBot.SelectionType mastSelectionType = MASTPlayoutBot.SelectionType.EGreedy,
            BudgetType budgetType = BudgetType.Iterations,
            int iterations = Constants.DEFAULT_COMPUTATION_ITERATION_BUDGET,
            long time = Constants.DEFAULT_COMPUTATION_TIME_BUDGET,
            int playoutTurnCutoff = Constants.DEFAULT_PLAYOUT_TURN_CUTOFF,
            double globalPolicy = Constants.DEFAULT_NMCTS_GLOBAL_POLICY,
            double localPolicy = Constants.DEFAULT_NMCTS_LOCAL_POLICY,
            bool useHeuristicEvaluation = false,
            bool debugInfoToConsole = false) {
            PerfectInformation = allowPerfectInformation;
            EnsembleSize = ensembleSize;
            PlayoutBotType = playoutBotType;
            MASTSelectionType = mastSelectionType;
            BudgetType = budgetType;
            Iterations = iterations;
            Time = time;
            PlayoutTurnCutoff = playoutTurnCutoff;
            GlobalPolicy = globalPolicy;
            LocalPolicy = localPolicy;
            _debug = debugInfoToConsole;

            // Create the ensemble search
            Ensemble = new EnsembleStrategySabberStone(enableStateObfuscation: true, enablePerfectInformation: PerfectInformation);

            // Simulation will be handled by the Playout
            var sabberStoneStateEvaluation = new EvaluationStrategyHearthStone(useHeuristicEvaluation);
            Playout = new PlayoutStrategySabberStone();

            // Set the playout bots
            switch (PlayoutBotType) {
                case PlayoutBotType.Random:
                    MyPlayoutBot = new RandomBot(filterDuplicatePositionTasks: true);
                    OpponentPlayoutBot = new RandomBot(filterDuplicatePositionTasks: true);
                    break;
                case PlayoutBotType.Heuristic:
                    MyPlayoutBot = new HeuristicBot();
                    OpponentPlayoutBot = new HeuristicBot();
                    break;
                case PlayoutBotType.MAST:
                    MyPlayoutBot = new MASTPlayoutBot(MASTSelectionType, sabberStoneStateEvaluation);
                    OpponentPlayoutBot = new MASTPlayoutBot(MASTSelectionType, sabberStoneStateEvaluation);
                    break;
                default:
                    throw new InvalidEnumArgumentException($"PlayoutBotType `{PlayoutBotType}' is not supported.");
            }

            // And the random sampling bot
            RandomSamplingBot = new RandomBot(filterDuplicatePositionTasks: true);

            // We'll be cutting off the simulations after X turns, using a GoalStrategy
            Goal = new GoalStrategyTurnCutoff(PlayoutTurnCutoff);

            // Application and Goal will be handled by the GameLogic
            GameLogic = new SabberStoneGameLogic(Goal, false);

            // Build NMCTS
            Builder = NMCTS<List<SabberStoneAction>, SabberStoneState, SabberStoneAction, object, SabberStoneAction>.Builder();
            Builder.ExplorationStrategy = new ChanceExploration<List<SabberStoneAction>, SabberStoneState, SabberStoneAction, object, SabberStoneAction>(LocalPolicy);
            Builder.PlayoutStrategy = Playout;
            Builder.PolicyGlobal = GlobalPolicy;
            Builder.SamplingStrategy = new SabberStoneNMCSamplingStrategy(RandomSamplingBot);
            Builder.SolutionStrategy = new SolutionStrategySabberStone(false, new AverageScore<SabberStoneState, SabberStoneAction>());
            Builder.EvaluationStrategy = sabberStoneStateEvaluation;
            switch (BudgetType) {
                case BudgetType.Iterations:
                    Builder.Iterations = EnsembleSize > 0 ? Iterations / EnsembleSize : Iterations; // Note: Integer division by design.
                    break;
                case BudgetType.Time:
                    Builder.Time = EnsembleSize > 0 ? Time / EnsembleSize : Time; // Note: Integer division by design.
                    break;
                default:
                    throw new InvalidEnumArgumentException($"BudgetType `{BudgetType}' is not supported.");
            }
        }

        #endregion

        #region Public Methods

        #region ISabberStoneBot

        /// <inheritdoc />
        public SabberStoneAction Act(SabberStoneState state) {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            var gameState = (SabberStoneState)state.Copy();

            if (_debug) Console.WriteLine();
            if (_debug) Console.WriteLine(Name());
            if (_debug) Console.WriteLine($"Starting a NMCTS search in turn {(gameState.Game.Turn + 1) / 2}");

            // Setup and start the ensemble-search
            EnsembleSolutions = new List<SabberStoneAction>();
            var search = (NMCTS<List<SabberStoneAction>, SabberStoneState, SabberStoneAction, object, SabberStoneAction>)Builder.Build();
            var context = SearchContext<List<SabberStoneAction>, SabberStoneState, SabberStoneAction, object, SabberStoneAction>.GameSearchSetup(GameLogic, EnsembleSolutions, gameState, null, search);
            Ensemble.EnsembleSearch(context, Searcher.Search, EnsembleSize);
            IterationsSpent = EnsembleSolutions.Sum(i => i.BudgetUsed);

            // Determine the best tasks to play based on the ensemble search, or just take the one in case of a single search.
            var solution = EnsembleSize > 1 ? Searcher.VoteForSolution(EnsembleSolutions, state) : EnsembleSolutions.First();

            if (_debug) Console.WriteLine();
            if (_debug) Console.WriteLine($"NMCTS returned with solution: {solution}");
            if (_debug) Console.WriteLine($"My total calculation time was: {timer.ElapsedMilliseconds} ms.");

            // Check if the solution is a complete action.
            if (!solution.IsComplete()) {
                // Otherwise add an End-Turn task before returning.
                if (_debug) Console.WriteLine("Solution was an incomplete action; adding End-Turn task.");
                solution.Tasks.Add((SabberStonePlayerTask)EndTurnTask.Any(Player));
            }

            if (_debug) Console.WriteLine();
            return solution;
        }

        /// <inheritdoc />
        public void SetController(Controller controller) {
            Player = controller;

            MyPlayoutBot.SetController(Player);
            OpponentPlayoutBot.SetController(Player.Opponent);

            // Set the playout bots correctly if we are using PlayoutStrategySabberStone
            Playout.AddPlayoutBot(Player.Id, MyPlayoutBot);
            Playout.AddPlayoutBot(Player.Opponent.Id, OpponentPlayoutBot);

            // Set the controller of the random sampling bot
            RandomSamplingBot.SetController(Player);

            // Create the searcher that will handle the searching and some administrative tasks
            Searcher = new SabberStoneSearch(Player, _debug);
        }

        /// <inheritdoc />
        public int PlayerID() {
            return Player.Id;
        }

        /// <inheritdoc />
        public string Name() {
            var it = Iterations != Constants.DEFAULT_COMPUTATION_ITERATION_BUDGET ? $"_{Iterations}it" : "";
            var ti = Time != Constants.DEFAULT_COMPUTATION_TIME_BUDGET ? $"_{Time}ti" : "";
            var gp = Math.Abs(GlobalPolicy - Constants.DEFAULT_NMCTS_GLOBAL_POLICY) > AVThesis.Constants.DOUBLE_EQUALITY_TOLERANCE ? $"_{GlobalPolicy}gp" : "";
            var lp = Math.Abs(LocalPolicy - Constants.DEFAULT_NMCTS_LOCAL_POLICY) > AVThesis.Constants.DOUBLE_EQUALITY_TOLERANCE ? $"_{LocalPolicy}lp" : "";
            var ptc = PlayoutTurnCutoff != Constants.DEFAULT_PLAYOUT_TURN_CUTOFF ? $"_{PlayoutTurnCutoff}tc" : "";
            var es = EnsembleSize > 1 ? $"_{EnsembleSize}es" : "";
            var pi = PerfectInformation ? "_PI" : "";
            return $"{BOT_NAME}{it}{ti}{gp}{lp}{ptc}{es}{pi}";
        }
        
        /// <inheritdoc />
        public long BudgetSpent() {
            return IterationsSpent;
        }

        /// <inheritdoc />
        public int MaxDepth() {
            return Searcher.CurrentMaxDepth;
        }

        #endregion

        #endregion

    }

}
