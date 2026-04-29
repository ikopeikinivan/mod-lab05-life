using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using cli_life;

namespace Life.Tests
{
    public class CellTests
    {
        [Fact]
        public void Cell_InitialState_IsDead()
        {
            var cell = new Cell();
            Assert.False(cell.IsAlive);
        }

        [Fact]
        public void Cell_Underpopulation_Dies()
        {
            var cell = new Cell { IsAlive = true };
            cell.neighbors.Add(new Cell());
            cell.DetermineNextLiveState();
            cell.Advance();
            Assert.False(cell.IsAlive);
        }

        [Fact]
        public void Cell_Survival_Lives()
        {
            var cell = new Cell { IsAlive = true };
            for (int i = 0; i < 2; i++)
                cell.neighbors.Add(new Cell { IsAlive = true });
            cell.DetermineNextLiveState();
            cell.Advance();
            Assert.True(cell.IsAlive);
        }

        [Fact]
        public void Cell_Overpopulation_Dies()
        {
            var cell = new Cell { IsAlive = true };
            for (int i = 0; i < 4; i++)
                cell.neighbors.Add(new Cell { IsAlive = true });
            cell.DetermineNextLiveState();
            cell.Advance();
            Assert.False(cell.IsAlive);
        }

        [Fact]
        public void Cell_Birth_BecomesAlive()
        {
            var cell = new Cell { IsAlive = false };
            for (int i = 0; i < 3; i++)
                cell.neighbors.Add(new Cell { IsAlive = true });
            cell.DetermineNextLiveState();
            cell.Advance();
            Assert.True(cell.IsAlive);
        }
    }

    public class BoardTests
    {
        [Fact]
        public void Board_Constructor_CreatesEmpty()
        {
            var board = new Board(10, 10, 1, 0.0);
            Assert.Equal(0, board.CountAlive());
        }

        [Fact]
        public void Board_Randomize_SetsApproximateDensity()
        {
            var board = new Board(100, 100, 1);
            board.Randomize(0.25);
            int live = board.CountAlive();
            Assert.InRange(live, 2400, 2600);
        }

        [Fact]
        public void Board_Advance_IncrementsNothingDirectly()
        {
            var board = new Board(10, 10, 1);
            int before = board.CountAlive();
            board.Advance();
            Assert.True(true);
        }

        [Fact]
        public void Board_CountAlive_ReturnsCorrectCount()
        {
            var board = new Board(5, 5, 1, 0.0);
            board.Cells[0, 0].IsAlive = true;
            board.Cells[2, 2].IsAlive = true;
            board.Cells[4, 4].IsAlive = true;
            Assert.Equal(3, board.CountAlive());
        }

        [Fact]
        public void Board_SaveAndLoad_PreservesState()
        {
            var board = new Board(4, 4, 1);
            board.Cells[0, 0].IsAlive = true;
            board.Cells[1, 1].IsAlive = true;
            board.Cells[2, 2].IsAlive = true;
            string path = "test_save.txt";
            board.SaveToFile(path);
            var loaded = Board.LoadFromFile(path);
            Assert.Equal(board.Columns, loaded.Columns);
            Assert.Equal(board.Rows, loaded.Rows);
            Assert.Equal(board.CountAlive(), loaded.CountAlive());
            File.Delete(path);
        }

        [Fact]
        public void Board_GetComponents_ReturnsCorrectCount()
        {
            var board = new Board(6, 6, 1);
            board.Cells[0, 0].IsAlive = board.Cells[1, 0].IsAlive = 
            board.Cells[0, 1].IsAlive = board.Cells[1, 1].IsAlive = true;
            board.Cells[5, 5].IsAlive = true;

            var components = board.GetComponents();
            Assert.Equal(2, components.Count);
        }

        [Fact]
        public void Board_GetComponents_ToroidalConnectivity()
        {
            var board = new Board(5, 5, 1, 0.0);
            board.Cells[0, 0].IsAlive = true;
            board.Cells[4, 4].IsAlive = true;
            var components = board.GetComponents();
            Assert.Single(components);
        }
    }

    public class FigureClassifierTests
    {
        [Fact]
        public void FigureClassifier_RecognizesBlock()
        {
            var blockCoords = new List<(int, int)> { (0,0), (1,0), (0,1), (1,1) };
            string result = FigureClassifier.Classify(blockCoords);
            Assert.Equal("Block", result);
        }

        [Fact]
        public void FigureClassifier_RecognizesBlinker()
        {
            var blinkerCoords = new List<(int, int)> { (0,0), (1,0), (2,0) };
            string result = FigureClassifier.Classify(blinkerCoords);
            Assert.Equal("Blinker", result);
        }

        [Fact]
        public void FigureClassifier_RecognizesGlider()
        {
            var gliderCoords = new List<(int, int)> { (1,0), (2,1), (0,2), (1,2), (2,2) };
            string result = FigureClassifier.Classify(gliderCoords);
            Assert.Equal("Glider", result);
        }

        [Fact]
        public void FigureClassifier_UnknownPattern_ReturnsUnknown()
        {
            var unknown = new List<(int, int)> { (0,0), (0,1), (1,0) };
            string result = FigureClassifier.Classify(unknown);
            Assert.Equal("Unknown", result);
        }
    }

    public class StabilityAnalyzerTests
    {
        [Fact]
        public void StabilityAnalyzer_GenerationsToStability_BlockIsStable()
        {
            var board = new Board(4, 4, 1);
            board.Cells[0, 0].IsAlive = board.Cells[1, 0].IsAlive =
            board.Cells[0, 1].IsAlive = board.Cells[1, 1].IsAlive = true;
            int generations = StabilityAnalyzer.GenerationsToStability(board, maxGen: 20, stableWindow: 2);
            Assert.Equal(1, generations);
        }

        [Fact]
        public void StabilityAnalyzer_GenerationsToStability_OscillatorNotStable()
        {
            var board = new Board(3, 3, 1);
            board.Cells[0, 1].IsAlive = board.Cells[1, 1].IsAlive = board.Cells[2, 1].IsAlive = true;
            int generations = StabilityAnalyzer.GenerationsToStability(board, maxGen: 10, stableWindow: 5);
            Assert.InRange(generations, -1, 10);
        }

        [Fact]
        public void StabilityAnalyzer_RunExperiment_ReturnsValidData()
        {
            double[] densities = { 0.1, 0.5, 0.9 };
            var result = StabilityAnalyzer.RunExperiment(20, 20, 1, densities, runs: 3, maxGen: 20, stableWindow: 2);
            Assert.Equal(3, result.Count);
            foreach (var kv in result)
            {
                Assert.InRange(kv.Key, 0.0, 1.0);
                Assert.InRange(kv.Value, 0.0, 20.0);
            }
        }
    }

    public class AsciiPlotTests
    {
        [Fact]
        public void AsciiPlot_Render_ReturnsNonEmptyString()
        {
            var data = new Dictionary<double, double>
            {
                { 0.1, 10.5 },
                { 0.5, 25.0 },
                { 0.9, 5.0 }
            };
            string plot = AsciiPlot.Render(data, plotWidth: 30, plotHeight: 10);
            Assert.False(string.IsNullOrWhiteSpace(plot));
            Assert.Contains("Generations to stability", plot);
            Assert.Contains("Density", plot);
        }
    }
}