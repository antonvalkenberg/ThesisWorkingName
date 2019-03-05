﻿using System;
using System.Collections.Generic;
using System.Linq;
using AVThesis.Datastructures;
using AVThesis.Game;
using AVThesis.SabberStone.Strategies;
using AVThesis.Search;
using AVThesis.Search.LSI;
using AVThesis.Search.Tree;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

/// <summary>
/// Written by A.J.J. Valkenberg, used in his Master Thesis on Artificial Intelligence.
/// In parts inspired by a code framework written by G.J. Roelofs and T. Aliyev.
/// </summary>
namespace AVThesis.SabberStone.Bots {

    /// <summary>
    /// A bot that plays Hearthstone using Linear Side Information to find its best move.
    /// </summary>
    public class LSIBot : ISabberStoneBot {

        #region Inner Classes

        private class SabberStoneSideInformationStrategy : ISideInformationStrategy<List<SabberStoneAction>, SabberStoneState, SabberStoneAction, object, SabberStoneAction, OddmentTable<SabberStonePlayerTask>> {

            #region Properties

            /// <summary>
            /// The bot that is used during the playouts.
            /// </summary>
            private RandomBot PlayoutBot { get; }
            
            /// <summary>
            /// The strategy used to play out a game in simulation.
            /// </summary>
            private IPlayoutStrategy<List<SabberStoneAction>, SabberStoneState, SabberStoneAction, object, SabberStoneAction> Playout { get; }

            /// <summary>
            /// The evaluation strategy for determining the value of samples.
            /// </summary>
            private IStateEvaluation<List<SabberStoneAction>, SabberStoneState, SabberStoneAction, object, SabberStoneAction, TreeSearchNode<SabberStoneState, SabberStoneAction>> Evaluation { get; }

            /// <summary>
            /// The game specific logic required for searching through SabberStoneStates and SabberStoneActions.
            /// </summary>
            private IGameLogic<List<SabberStoneAction>, SabberStoneState, SabberStoneAction, object, SabberStoneAction, SabberStoneAction> GameLogic { get; }

            #endregion

            #region Constructor

            /// <summary>
            /// Creates a new instance.
            /// </summary>
            /// <param name="playout">The strategy used to play out a game in simulation.</param>
            /// <param name="evaluation">The evaluation strategy for determining the value of samples.</param>
            /// <param name="gameLogic">The game specific logic required for searching through SabberStoneStates and SabberStoneActions.</param>
            public SabberStoneSideInformationStrategy(IPlayoutStrategy<List<SabberStoneAction>, SabberStoneState, SabberStoneAction, object, SabberStoneAction> playout, IStateEvaluation<List<SabberStoneAction>, SabberStoneState, SabberStoneAction, object, SabberStoneAction, TreeSearchNode<SabberStoneState, SabberStoneAction>> evaluation, IGameLogic<List<SabberStoneAction>, SabberStoneState, SabberStoneAction, object, SabberStoneAction, SabberStoneAction> gameLogic) {
                PlayoutBot = new RandomBot();
                Playout = playout;
                Evaluation = evaluation;
                GameLogic = gameLogic;
            }

            #endregion

            #region Public Methods

            /// <inheritdoc />
            public OddmentTable<SabberStonePlayerTask> Create(SearchContext<List<SabberStoneAction>, SabberStoneState, SabberStoneAction, object, SabberStoneAction> context, int samplesForGeneration) {

                // So we have an issue here, because it's Hearthstone we don't know in advance how many dimensions we have.
                //      -> We can just evenly distribute budget over the currently available dimensions
                //      -- Reason is that some dimensions would be `locked' until after a specific dimensions is explored
                //      -> Another option is to expand all possible sequences, this might be too complex though...

                // In Hearthstone we don't really have multiple available actions per dimension
                // It's more like you either play/attack or not
                // Although each minion can have multiple choices on what to attack

                // We can still try to distillate individual actions
                // It might be best to keep track of these through a dictionary/array
                //      So we'd randomly choose actions and simulate when `end-turn' is chosen
                //      And then update the value of any selected PlayerTask

                // I guess use OddmentTable again?

                var table = new Dictionary<SabberStonePlayerTask, double>(PlayerTaskComparer.Comparer);

                // So, we have a number of samples to use
                // For each of those, generate a random SabberStoneAction and playout
                for (var i = 0; i < samplesForGeneration; i++) {

                    var action = PlayoutBot.CreateRandomAction(context.Source);

                    var newState = GameLogic.Apply(context, (SabberStoneState)context.Source.Copy(), action);

                    var endState = Playout.Playout(context, newState);

                    var value = Evaluation.Evaluate(context, null, endState);

                    foreach (var task in action.Tasks) {
                        if (!table.ContainsKey(task)) table.Add(task, 0);
                        table[task] += value;
                    }
                }

                // Create the Oddment table
                var oddmentTable = new OddmentTable<SabberStonePlayerTask>();
                foreach (var kvPair in table) {
                    oddmentTable.Add(kvPair.Key, kvPair.Value, recalculate: false);
                }

                oddmentTable.Recalculate();

                return oddmentTable;
            }

            #endregion

        }

        private class SabberStoneLSISamplingStrategy : ILSISamplingStrategy<SabberStoneState, SabberStoneAction, OddmentTable<SabberStonePlayerTask>> {
            
            #region Properties

            /// <summary>
            /// The game specific logic required for searching through SabberStoneStates and SabberStoneActions.
            /// </summary>
            private IGameLogic<List<SabberStoneAction>, SabberStoneState, SabberStoneAction, object, SabberStoneAction, SabberStoneAction> GameLogic { get; }

            #endregion

            #region Constructor

            /// <summary>
            /// Constructs a new instance.
            /// </summary>
            /// <param name="gameLogic">The game specific logic required for searching through SabberStoneStates and SabberStoneActions.</param>
            public SabberStoneLSISamplingStrategy(IGameLogic<List<SabberStoneAction>, SabberStoneState, SabberStoneAction, object, SabberStoneAction, SabberStoneAction> gameLogic) {
                GameLogic = gameLogic;
            }

            #endregion

            #region Private Methods

            /// <summary>
            /// Expands a specific state and returns all available SabberStonePlayerTasks.
            /// Note: it is assumed that the expansion is performed Hierarchically and therefore only contains single SabberStonePlayerTask SabberStoneActions.
            /// </summary>
            /// <param name="state">The game state.</param>
            /// <returns>Collection of SabberStonePlayerTasks that are available in the provided state.</returns>
            private IEnumerable<SabberStonePlayerTask> GetAvailablePlayerTasks(SabberStoneState state) {
                return GameLogic.Expand(null, state).Select(expandedAction => expandedAction.Tasks.First()).ToList();
            }

            #endregion
            
            #region Public Methods

            /// <inheritdoc />
            public SabberStoneAction Sample(SabberStoneState state, OddmentTable<SabberStonePlayerTask> sideInformation) {

                var action = new SabberStoneAction();
                while (!action.IsComplete()) {
                    // Sample a task from the OddmentTable
                    var task = sideInformation.Next();
                    // Check if the task is available in the current state
                    var availableTasks = GetAvailablePlayerTasks(state);
                    if (!availableTasks.Contains(task, PlayerTaskComparer.Comparer)) continue;
                    action.AddTask(task);
                    state.Game.Process(task.Task);
                }

                return action;
            }

            /// <inheritdoc />
            public SabberStoneAction Sample(SabberStoneState state) {
                return SabberStoneAction.CreateNullMove(state.CurrentPlayer() == state.Player1.Id ? state.Player1 : state.Player2);
            }

            #endregion
            
        }

        #endregion

        #region Constants

        private const string BOT_NAME = "LSIBot";
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
        public ISabberStoneBot MyPlayoutBot { get; set; }

        /// <summary>
        /// The bot that is used for the opponent's playouts.
        /// </summary>
        public ISabberStoneBot OpponentPlayoutBot { get; set; }

        /// <summary>
        /// The amount of turns after which to stop a simulation.
        /// </summary>
        public int PlayoutTurnCutoff { get; set; }

        /// <summary>
        /// The strategy used to determine if a playout has reached its goal state.
        /// </summary>
        public IGoalStrategy<List<SabberStoneAction>, SabberStoneState, SabberStoneAction, object, SabberStoneAction> Goal { get; set; }

        /// <summary>
        /// The strategy used to play out a game in simulation.
        /// </summary>
        public IPlayoutStrategy<List<SabberStoneAction>, SabberStoneState, SabberStoneAction, object, SabberStoneAction> Playout { get; set; }

        /// <summary>
        /// The evaluation strategy for determining the value of samples.
        /// </summary>
        public IStateEvaluation<List<SabberStoneAction>, SabberStoneState, SabberStoneAction, object, SabberStoneAction, TreeSearchNode<SabberStoneState, SabberStoneAction>> Evaluation { get; set; }

        /// <summary>
        /// The game specific logic required for searching through SabberStoneStates and SabberStoneActions
        /// </summary>
        public IGameLogic<List<SabberStoneAction>, SabberStoneState, SabberStoneAction, object, SabberStoneAction, SabberStoneAction> GameLogic { get; set; }
        
        /// <summary>
        /// The strategy for creating the side information.
        /// </summary>
        public ISideInformationStrategy<List<SabberStoneAction>, SabberStoneState, SabberStoneAction, object, SabberStoneAction, OddmentTable<SabberStonePlayerTask>> SideInformationStrategy { get; set; }

        /// <summary>
        /// The strategy for sampling actions during the generation phase of LSI.
        /// </summary>
        public ILSISamplingStrategy<SabberStoneState, SabberStoneAction, OddmentTable<SabberStonePlayerTask>> SamplingStrategy { get; set; }

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
        /// The type of selection strategy used by the M.A.S.T. playout.
        /// </summary>
        public MASTPlayoutBot.SelectionType MASTSelectionType { get; set; }

        /// <summary>
        /// The amount of samples to use during the generation phase.
        /// </summary>
        public int SamplesForGeneration { get; set; }

        /// <summary>
        /// The amount of samples to use during the evaluation phase.
        /// </summary>
        public int SamplesForEvaluation { get; set; }

        #endregion

        #region Constructor

        /// <summary>
        /// Constructs a new instance of LSIBot with a <see cref="Controller"/> representing the player.
        /// </summary>
        /// <param name="player">The player.</param>
        /// <param name="allowPerfectInformation">[Optional] Whether or not this bot is allowed perfect information about the game state (i.e. no obfuscation and therefore no determinisation). Default value is false.</param>
        /// <param name="ensembleSize">[Optional] The size of the ensemble to use. Default value is 1.</param>
        /// <param name="mastSelectionType">[Optional] The type of selection strategy used by the M.A.S.T. playout. Default value is <see cref="MASTPlayoutBot.SelectionType.EGreedy"/>.</param>
        /// <param name="playoutTurnCutoff">[Optional] The amount of turns after which to stop a simulation. Default value is <see cref="Constants.DEFAULT_PLAYOUT_TURN_CUTOFF"/>.</param>
        /// <param name="samplesForGeneration">[Optional] The amount of samples to use during the generation phase. Default value is <see cref="Constants.DEFAULT_LSI_SAMPLES_FOR_GENERATION"/>.</param>
        /// <param name="samplesForEvaluation">[Optional] The amount of samples to use during the evaluation phase. Default value is <see cref="Constants.DEFAULT_LSI_SAMPLES_FOR_EVALUATION"/> * <see cref="Constants.DEFAULT_LSI_EVALUATION_SAMPLES_ADJUSTMENT_FACTOR"/>.</param>
        /// <param name="debugInfoToConsole">[Optional] Whether or not to write debug information to the console. Default value is false.</param>
        public LSIBot(Controller player, bool allowPerfectInformation = false, int ensembleSize = 1, MASTPlayoutBot.SelectionType mastSelectionType = MASTPlayoutBot.SelectionType.EGreedy, int playoutTurnCutoff = Constants.DEFAULT_PLAYOUT_TURN_CUTOFF, int samplesForGeneration = (int)(Constants.DEFAULT_COMPUTATION_ITERATION_BUDGET * Constants.DEFAULT_LSI_BUDGET_GENERATION_PERCENTAGE), int samplesForEvaluation = (int)(Constants.DEFAULT_COMPUTATION_ITERATION_BUDGET * Constants.DEFAULT_LSI_BUDGET_EVALUATION_PERCENTAGE * Constants.DEFAULT_LSI_EVALUATION_SAMPLES_ADJUSTMENT_FACTOR), bool debugInfoToConsole = false)
            : this(allowPerfectInformation, ensembleSize, mastSelectionType, playoutTurnCutoff, samplesForGeneration, samplesForEvaluation, debugInfoToConsole) {
            SetController(player);
        }

        /// <summary>
        /// Constructs a new instance of LSIBot with default strategies.
        /// </summary>
        /// <param name="allowPerfectInformation">[Optional] Whether or not this bot is allowed perfect information about the game state (i.e. no obfuscation and therefore no determinisation). Default value is false.</param>
        /// <param name="ensembleSize">[Optional] The size of the ensemble to use. Default value is 1.</param>
        /// <param name="mastSelectionType">[Optional] The type of selection strategy used by the M.A.S.T. playout. Default value is <see cref="MASTPlayoutBot.SelectionType.EGreedy"/>.</param>
        /// <param name="playoutTurnCutoff">[Optional] The amount of turns after which to stop a simulation. Default value is <see cref="Constants.DEFAULT_PLAYOUT_TURN_CUTOFF"/>.</param>
        /// <param name="samplesForGeneration">[Optional] The amount of samples to use during the generation phase. Default value is <see cref="Constants.DEFAULT_LSI_SAMPLES_FOR_GENERATION"/>.</param>
        /// <param name="samplesForEvaluation">[Optional] The amount of samples to use during the evaluation phase. Default value is <see cref="Constants.DEFAULT_LSI_SAMPLES_FOR_EVALUATION"/> * <see cref="Constants.DEFAULT_LSI_EVALUATION_SAMPLES_ADJUSTMENT_FACTOR"/>.</param>
        /// <param name="debugInfoToConsole">[Optional] Whether or not to write debug information to the console. Default value is false.</param>
        public LSIBot(bool allowPerfectInformation = false, int ensembleSize = 1, MASTPlayoutBot.SelectionType mastSelectionType = MASTPlayoutBot.SelectionType.EGreedy, int playoutTurnCutoff = Constants.DEFAULT_PLAYOUT_TURN_CUTOFF, int samplesForGeneration = (int)(Constants.DEFAULT_COMPUTATION_ITERATION_BUDGET * Constants.DEFAULT_LSI_BUDGET_GENERATION_PERCENTAGE), int samplesForEvaluation = (int)(Constants.DEFAULT_COMPUTATION_ITERATION_BUDGET * Constants.DEFAULT_LSI_BUDGET_EVALUATION_PERCENTAGE * Constants.DEFAULT_LSI_EVALUATION_SAMPLES_ADJUSTMENT_FACTOR), bool debugInfoToConsole = false) {
            PerfectInformation = allowPerfectInformation;
            EnsembleSize = ensembleSize;
            MASTSelectionType = mastSelectionType;
            PlayoutTurnCutoff = playoutTurnCutoff;
            SamplesForGeneration = samplesForGeneration;
            SamplesForEvaluation = samplesForEvaluation;
            _debug = debugInfoToConsole;

            // Create the ensemble search
            Ensemble = new EnsembleStrategySabberStone(enableStateObfuscation: true, enablePerfectInformation: PerfectInformation);

            // Simulation will be handled by the Playout.
            var sabberStoneStateEvaluation = new EvaluationStrategyHearthStone();
            var playout = new PlayoutStrategySabberStone();
            Playout = playout;

            // Set the playout bots
            MyPlayoutBot = new MASTPlayoutBot(MASTSelectionType, sabberStoneStateEvaluation, playout);
            OpponentPlayoutBot = new MASTPlayoutBot(MASTSelectionType, sabberStoneStateEvaluation, playout);

            // LSI will need a goal-strategy to determine when a simulation is done
            Goal = new GoalStrategyTurnCutoff(PlayoutTurnCutoff);

            // LSI will need an evaluation-strategy to evaluate the strength of samples
            Evaluation = new EvaluationStrategyHearthStone();

            // Application will be handled by the GameLogic
            // Hierarchical Expansion is set to TRUE because of incremental task validation during action sampling.
            GameLogic = new SabberStoneGameLogic(Goal, hierarchicalExpansion: true);

            // The side information strategy needs access to several of these.
            SideInformationStrategy = new SabberStoneSideInformationStrategy(Playout, Evaluation, GameLogic);

            // The sampling strategy used to sample actions during the generation phase.
            SamplingStrategy = new SabberStoneLSISamplingStrategy(GameLogic);
        }

        #endregion

        #region Public Methods

        #region ISabberStoneBot

        /// <inheritdoc />
        public SabberStoneAction Act(SabberStoneState state) {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            var stateCopy = (SabberStoneState)state.Copy();

            if (_debug) Console.WriteLine();
            if (_debug) Console.WriteLine(Name());
            if (_debug) Console.WriteLine($"Starting an LSI search in turn {(stateCopy.Game.Turn + 1) / 2}");
            
            // Adjust sample sizes again for use in the Ensemble
            var samplesForEvaluation = EnsembleSize > 0 ? SamplesForEvaluation / EnsembleSize : SamplesForEvaluation; // Note: Integer division by design.
            var samplesForGeneration = EnsembleSize > 0 ? SamplesForGeneration / EnsembleSize : SamplesForGeneration; // Note: Integer division by design.

            // Create a new LSI search
            var search = new LSI<List<SabberStoneAction>, SabberStoneState, SabberStoneAction, object, TreeSearchNode<SabberStoneState, SabberStoneAction>, OddmentTable<SabberStonePlayerTask>>(
                samplesForGeneration,
                samplesForEvaluation,
                SideInformationStrategy,
                SamplingStrategy,
                Playout,
                Evaluation,
                GameLogic);
            
            // Reset the solutions collection
            EnsembleSolutions = new List<SabberStoneAction>();

            // Create a SearchContext that just holds the current state as Source and the Search.
            var context = SearchContext<List<SabberStoneAction>, SabberStoneState, SabberStoneAction, object, SabberStoneAction>.Context(EnsembleSolutions, stateCopy, null, null, search, null);
            // The Playout strategy will call the Goal strategy from the context, so we set it here
            context.Goal = Goal;

            // Execute the search
            Ensemble.EnsembleSearch(context, Searcher.Search, EnsembleSize);

            // Determine a solution
            var solution = Searcher.VoteForSolution(EnsembleSolutions, state);

            var time = timer.ElapsedMilliseconds;
            if (_debug) Console.WriteLine();
            if (_debug) Console.WriteLine($"LSI returned with solution: {solution}");
            if (_debug) Console.WriteLine($"My total calculation time was: {time} ms");
            if (_debug) Console.WriteLine($"Actual evaluation samples used during last search: {search.SamplesUsedEvaluation}");

            // Check if the solution is a complete action.
            if (!solution.IsComplete()) {
                // Otherwise add an End-Turn task before returning.
                Console.WriteLine("Solution was an incomplete action; adding End-Turn task.");
                solution.Tasks.Add((SabberStonePlayerTask)EndTurnTask.Any(Player));
            }

            if (_debug) Console.WriteLine();
            return solution;
        }

        /// <inheritdoc />
        public void SetController(Controller controller) {
            Player = controller;

            // Set the playout bots correctly if we are using PlayoutStrategySabberStone
            if (Playout is PlayoutStrategySabberStone playout) {
                MyPlayoutBot.SetController(Player);
                playout.AddPlayoutBot(Player.Id, MyPlayoutBot);
                OpponentPlayoutBot.SetController(Player.Opponent);
                playout.AddPlayoutBot(Player.Id, MyPlayoutBot);
            }

            // Create the searcher that will handle the searching and some administrative tasks
            Searcher = new SabberStoneSearch(Player, _debug);
        }

        /// <inheritdoc />
        public int PlayerID() {
            return Player.Id;
        }

        /// <inheritdoc />
        public string Name() {
            var sfg = $"_{SamplesForGeneration}g";
            var sfe = $"_{SamplesForEvaluation}e";
            var ptc = PlayoutTurnCutoff != Constants.DEFAULT_PLAYOUT_TURN_CUTOFF ? $"_{PlayoutTurnCutoff}tc" : "";
            var es = EnsembleSize > 1 ? $"_{EnsembleSize}es" : "";
            var pi = PerfectInformation ? "_PI" : "";
            return $"{BOT_NAME}{sfg}{sfe}{ptc}{es}{pi}";
        }

        #endregion

        #endregion

    }
}
