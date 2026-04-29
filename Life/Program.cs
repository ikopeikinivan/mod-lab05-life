using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Text.Json;
using ScottPlot;

namespace cli_life
{
    public class GameSettings
    {
        public int Width { get; set; } = 50;
        public int Height { get; set; } = 20;
        public int CellSize { get; set; } = 1;
        public double Density { get; set; } = 0.5;
        public int SleepMs { get; set; } = 1000;

        public static GameSettings Load(string path)
        {
            if (!File.Exists(path)) return new GameSettings();
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<GameSettings>(json) ?? new GameSettings();
        }

        public void Save(string path)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(path, JsonSerializer.Serialize(this, options));
        }
    }

    public class Cell
    {
        public bool IsAlive;
        public readonly List<Cell> neighbors = new List<Cell>();
        private bool IsAliveNext;

        public void DetermineNextLiveState()
        {
            int liveNeighbors = neighbors.Count(x => x.IsAlive);
            if (IsAlive)
                IsAliveNext = liveNeighbors == 2 || liveNeighbors == 3;
            else
                IsAliveNext = liveNeighbors == 3;
        }

        public void Advance()
        {
            IsAlive = IsAliveNext;
        }
    }

    public class Board
    {
        public readonly Cell[,] Cells;
        public readonly int CellSize;
        public int Columns => Cells.GetLength(0);
        public int Rows => Cells.GetLength(1);
        public int Width => Columns * CellSize;
        public int Height => Rows * CellSize;

        private readonly Random rand = new Random();

        public Board(int width, int height, int cellSize, double liveDensity = 0.1)
        {
            CellSize = cellSize;
            Cells = new Cell[width / cellSize, height / cellSize];
            for (int x = 0; x < Columns; x++)
                for (int y = 0; y < Rows; y++)
                    Cells[x, y] = new Cell();
            ConnectNeighbors();
            Randomize(liveDensity);
        }

        public void Randomize(double liveDensity)
        {
            foreach (var cell in Cells)
                cell.IsAlive = rand.NextDouble() < liveDensity;
        }

        public void Advance()
        {
            foreach (var cell in Cells)
                cell.DetermineNextLiveState();
            foreach (var cell in Cells)
                cell.Advance();
        }

        private void ConnectNeighbors()
        {
            for (int x = 0; x < Columns; x++)
            {
                for (int y = 0; y < Rows; y++)
                {
                    int xL = (x > 0) ? x - 1 : Columns - 1;
                    int xR = (x < Columns - 1) ? x + 1 : 0;
                    int yT = (y > 0) ? y - 1 : Rows - 1;
                    int yB = (y < Rows - 1) ? y + 1 : 0;

                    var cell = Cells[x, y];
                    cell.neighbors.Add(Cells[xL, yT]);
                    cell.neighbors.Add(Cells[x, yT]);
                    cell.neighbors.Add(Cells[xR, yT]);
                    cell.neighbors.Add(Cells[xL, y]);
                    cell.neighbors.Add(Cells[xR, y]);
                    cell.neighbors.Add(Cells[xL, yB]);
                    cell.neighbors.Add(Cells[x, yB]);
                    cell.neighbors.Add(Cells[xR, yB]);
                }
            }
        }

        public int CountAlive() => Cells.Cast<Cell>().Count(c => c.IsAlive);

        public void SaveToFile(string path)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"{Columns} {Rows} {CellSize}");
            for (int row = 0; row < Rows; row++)
            {
                for (int col = 0; col < Columns; col++)
                    sb.Append(Cells[col, row].IsAlive ? '1' : '0');
                sb.AppendLine();
            }
            File.WriteAllText(path, sb.ToString());
        }

        public static Board LoadFromFile(string path)
        {
            var lines = File.ReadAllLines(path);
            var header = lines[0].Split(' ');
            int cols = int.Parse(header[0]);
            int rows = int.Parse(header[1]);
            int cellSize = int.Parse(header[2]);

            var board = new Board(cols * cellSize, rows * cellSize, cellSize, 0.0);
            for (int row = 0; row < rows && row + 1 < lines.Length; row++)
            {
                string line = lines[row + 1];
                for (int col = 0; col < cols && col < line.Length; col++)
                    board.Cells[col, row].IsAlive = (line[col] == '1');
            }
            return board;
        }

        public List<List<(int x, int y)>> GetComponents()
        {
            var visited = new bool[Columns, Rows];
            var components = new List<List<(int, int)>>();

            for (int x = 0; x < Columns; x++)
            {
                for (int y = 0; y < Rows; y++)
                {
                    if (Cells[x, y].IsAlive && !visited[x, y])
                    {
                        var component = new List<(int, int)>();
                        var queue = new Queue<(int, int)>();
                        queue.Enqueue((x, y));
                        visited[x, y] = true;
                        while (queue.Count > 0)
                        {
                            var (cx, cy) = queue.Dequeue();
                            component.Add((cx, cy));
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                for (int dy = -1; dy <= 1; dy++)
                                {
                                    if (dx == 0 && dy == 0) continue;
                                    int nx = (cx + dx + Columns) % Columns;
                                    int ny = (cy + dy + Rows) % Rows;
                                    if (Cells[nx, ny].IsAlive && !visited[nx, ny])
                                    {
                                        visited[nx, ny] = true;
                                        queue.Enqueue((nx, ny));
                                    }
                                }
                            }
                        }
                        components.Add(component);
                    }
                }
            }
            return components;
        }
    }

    public static class FigureClassifier
    {
        public static readonly Dictionary<string, List<(int dx, int dy)>> Patterns = new()
        {
            ["Block"]   = new() { (0,0),(1,0),(0,1),(1,1) },
            ["Beehive"] = new() { (1,0),(2,0),(0,1),(3,1),(1,2),(2,2) },
            ["Loaf"]    = new() { (1,0),(2,0),(0,1),(3,1),(1,2),(3,2),(2,3) },
            ["Boat"]    = new() { (0,0),(1,0),(0,1),(2,1),(1,2) },
            ["Tub"]     = new() { (1,0),(0,1),(2,1),(1,2) },
            ["Blinker"] = new() { (0,0),(1,0),(2,0) },
            ["Glider"]  = new() { (1,0),(2,1),(0,2),(1,2),(2,2) },
        };

        public static string Classify(List<(int x, int y)> component)
        {
            if (component.Count == 0) return "Empty";
            int minX = component.Min(c => c.x);
            int minY = component.Min(c => c.y);
            var normalized = component
                .Select(c => (c.x - minX, c.y - minY))
                .OrderBy(c => c.Item1).ThenBy(c => c.Item2)
                .ToList();

            foreach (var (name, pattern) in Patterns)
            {
                if (MatchesPattern(normalized, pattern))
                    return name;
            }
            return "Unknown";
        }

        private static bool MatchesPattern(List<(int, int)> normalized, List<(int dx, int dy)> pattern)
        {
            if (normalized.Count != pattern.Count) return false;
            for (int rot = 0; rot < 4; rot++)
            {
                var rotated = Rotate(pattern, rot);
                int mx = rotated.Min(c => c.Item1);
                int my = rotated.Min(c => c.Item2);
                var normPattern = rotated
                    .Select(c => (c.Item1 - mx, c.Item2 - my))
                    .OrderBy(c => c.Item1).ThenBy(c => c.Item2)
                    .ToList();
                if (normalized.SequenceEqual(normPattern)) return true;
            }
            return false;
        }

        private static List<(int, int)> Rotate(List<(int dx, int dy)> pts, int times)
        {
            var result = pts.Select(p => (p.dx, p.dy)).ToList();
            for (int i = 0; i < times; i++)
                result = result.Select(p => (-p.Item2, p.Item1)).ToList();
            return result;
        }
    }

public static class StabilityAnalyzer
{
    public static int GenerationsToStability(Board board, int maxGen = 500, int stableWindow = 10)
    {
        int prevCount = board.CountAlive();
        int stableFor = 0;

        for (int gen = 1; gen <= maxGen; gen++)
        {
            board.Advance();
            int count = board.CountAlive();

            if (count == prevCount)
            {
                stableFor++;
                if (stableFor >= stableWindow)
                    return gen - stableWindow + 1;
            }
            else
            {
                stableFor = 0;
                prevCount = count;
            }
        }
        return -1;
    }

    public static Dictionary<double, double> RunExperiment(
        int width, int height, int cellSize,
        double[] densities, int runs = 20, int maxGen = 500, int stableWindow = 10)
    {
        var result = new Dictionary<double, double>();
        foreach (double density in densities)
        {
            int total = 0, counted = 0;
            for (int i = 0; i < runs; i++)
            {
                var b = new Board(width, height, cellSize, density);
                int gen = GenerationsToStability(b, maxGen, stableWindow);
                if (gen >= 0) { total += gen; counted++; }
            }
            result[density] = counted > 0 ? (double)total / counted : maxGen;
        }
        return result;
    }
}

    public static class AsciiPlot
    {
        public static string Render(Dictionary<double, double> data, int plotWidth = 60, int plotHeight = 20)
        {
            var xs = data.Keys.OrderBy(k => k).ToList();
            var ys = xs.Select(x => data[x]).ToList();

            double minY = ys.Min();
            double maxY = ys.Max();
            double rangeY = maxY - minY == 0 ? 1 : maxY - minY;

            var grid = new char[plotHeight, plotWidth];
            for (int r = 0; r < plotHeight; r++)
                for (int c = 0; c < plotWidth; c++)
                    grid[r, c] = ' ';

            for (int r = 0; r < plotHeight; r++) grid[r, 0] = '|';
            for (int c = 0; c < plotWidth; c++) grid[plotHeight - 1, c] = '-';
            grid[plotHeight - 1, 0] = '+';

            for (int i = 0; i < xs.Count; i++)
            {
                int col = 1 + (int)((double)i / (xs.Count - 1) * (plotWidth - 2));
                int row = (int)((1 - (ys[i] - minY) / rangeY) * (plotHeight - 2));
                row = Math.Clamp(row, 0, plotHeight - 2);
                col = Math.Clamp(col, 1, plotWidth - 1);
                grid[row, col] = '*';
            }

            var sb = new StringBuilder();
            sb.AppendLine($"  Generations to stability  [max={maxY:F0}]");
            for (int r = 0; r < plotHeight; r++)
            {
                if (r == 0) sb.Append($"{maxY,5:F0} ");
                else if (r == plotHeight - 1) sb.Append($"{minY,5:F0} ");
                else sb.Append("      ");
                sb.AppendLine(new string(Enumerable.Range(0, plotWidth).Select(c => grid[r, c]).ToArray()));
            }
            sb.Append("      ");
            sb.Append(xs[0].ToString("F2").PadRight(plotWidth / 2));
            sb.AppendLine(xs[^1].ToString("F2").PadLeft(plotWidth / 2));
            sb.AppendLine("                         Density");
            return sb.ToString();
        }
    }

    public class Program
    {
        static Board board = null!;
        static GameSettings settings = new GameSettings();

        static void Reset()
        {
            board = new Board(settings.Width, settings.Height, settings.CellSize, settings.Density);
        }

        static void Render()
        {
            for (int row = 0; row < board.Rows; row++)
            {
                for (int col = 0; col < board.Columns; col++)
                    Console.Write(board.Cells[col, row].IsAlive ? '*' : ' ');
                Console.WriteLine();
            }
        }

        static void PrintMenu()
        {
            Console.WriteLine("\n=== Game of Life ===");
            Console.WriteLine("1 - Запустить симуляцию");
            Console.WriteLine("2 - Сохранить состояние");
            Console.WriteLine("3 - Загрузить состояние из файла");
            Console.WriteLine("4 - Загрузить фигуру (colonies/)");
            Console.WriteLine("5 - Анализ поля (компоненты + классификация)");
            Console.WriteLine("6 - Исследование стабилизации (эксперимент)");
            Console.WriteLine("7 - Создать/пересоздать поле");
            Console.WriteLine("0 - Выход");
            Console.Write("Выбор: ");
        }

        static string GetSolutionRoot()
        {
            var dir = AppDomain.CurrentDomain.BaseDirectory;
            while (dir != null)
            {
                if (Directory.GetFiles(dir, "*.sln").Length > 0)
                    return dir;
                dir = Directory.GetParent(dir)?.FullName;
            }
            return AppDomain.CurrentDomain.BaseDirectory;
        }

        static void Main(string[] args)
        {
            string settingsPath = "settings.json";
            settings = GameSettings.Load(settingsPath);
            settings.Save(settingsPath);
            Reset();

            while (true)
            {
                PrintMenu();
                string input = Console.ReadLine() ?? "";
                switch (input.Trim())
                {
                    case "1": RunSimulation(); break;
                    case "2": SaveState(); break;
                    case "3": LoadState(); break;
                    case "4": LoadColony(); break;
                    case "5": AnalyzeField(); break;
                    case "6": RunStabilityExperiment(); break;
                    case "7": Reset(); Console.WriteLine("Поле пересоздано."); break;
                    case "0": return;
                    default: Console.WriteLine("Неверный выбор."); break;
                }
            }
        }

        static void RunSimulation()
        {
            Console.Write("Число шагов (0 = бесконечно, Esc для остановки): ");
            int steps = int.TryParse(Console.ReadLine(), out int s) ? s : 0;
            bool infinite = steps == 0;

            for (int i = 0; infinite || i < steps; i++)
            {
                Console.Clear();
                Render();
                Console.WriteLine($"Поколение: {i + 1}  Живых: {board.CountAlive()}");
                board.Advance();
                Thread.Sleep(settings.SleepMs);

                if (!infinite && i == steps - 1) break;
                if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape)
                    break;
            }
        }

        static void SaveState()
        {
            Console.Write("Имя файла (Enter = state.txt): ");
            string file = Console.ReadLine()?.Trim() ?? "";
            if (string.IsNullOrEmpty(file)) file = "state.txt";
            board.SaveToFile(file);
            Console.WriteLine($"Состояние сохранено в {file}");
        }

        static void LoadState()
        {
            Console.Write("Имя файла: ");
            string file = Console.ReadLine()?.Trim() ?? "";
            if (!File.Exists(file)) { Console.WriteLine("Файл не найден."); return; }
            board = Board.LoadFromFile(file);
            Console.WriteLine("Состояние загружено.");
        }

        static void LoadColony()
        {
            string dir = "colonies";
            if (!Directory.Exists(dir)) { Console.WriteLine("Папка colonies/ не найдена."); return; }

            var files = Directory.GetFiles(dir, "*.txt");
            if (files.Length == 0) { Console.WriteLine("Нет файлов колоний."); return; }

            Console.WriteLine("Доступные колонии:");
            for (int i = 0; i < files.Length; i++)
                Console.WriteLine($"  {i + 1}. {Path.GetFileName(files[i])}");

            Console.Write("Выберите номер: ");
            if (!int.TryParse(Console.ReadLine(), out int idx) || idx < 1 || idx > files.Length)
            { Console.WriteLine("Неверный номер."); return; }

            board = Board.LoadFromFile(files[idx - 1]);
            Console.WriteLine($"Загружена колония: {Path.GetFileName(files[idx - 1])}");
        }

        static void AnalyzeField()
        {
            var components = board.GetComponents();
            Console.WriteLine($"\nЖивых клеток: {board.CountAlive()}");
            Console.WriteLine($"Связных компонент: {components.Count}");

            var counts = new Dictionary<string, int>();
            foreach (var comp in components)
            {
                string name = FigureClassifier.Classify(comp);
                counts.TryGetValue(name, out int cnt);
                counts[name] = cnt + 1;
            }

            Console.WriteLine("Классификация:");
            foreach (var (name, cnt) in counts.OrderByDescending(kv => kv.Value))
                Console.WriteLine($"  {name}: {cnt}");
        }

        static void RunStabilityExperiment()
        {
            Console.WriteLine("Запуск эксперимента по стабилизации...");
            double[] densities = { 0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9 };
            var result = StabilityAnalyzer.RunExperiment(
                settings.Width, settings.Height, settings.CellSize,
                densities, runs: 20, maxGen: 500, stableWindow: 10);

            string root = GetSolutionRoot();
            string dataDir = Path.Combine(root, "Data");
            Directory.CreateDirectory(dataDir);

            string dataPath = Path.Combine(dataDir, "data.txt");
            var sb = new StringBuilder();
            sb.AppendLine("density\tavg_generations");
            foreach (var (d, g) in result.OrderBy(kv => kv.Key))
                sb.AppendLine($"{d:F2}\t{g:F2}");
            File.WriteAllText(dataPath, sb.ToString());
            Console.WriteLine($"Данные сохранены в {dataPath}");

            try
            {
                double[] xArr = result.Keys.OrderBy(k => k).ToArray();
                double[] yArr = xArr.Select(k => result[k]).ToArray();

                var plt = new Plot();
                plt.Add.Scatter(xArr, yArr);
                plt.Axes.Bottom.Label.Text = "Density";
                plt.Axes.Left.Label.Text = "Avg Generations to Stability";
                plt.Axes.Title.Label.Text = "Game of Life: Stabilization";

                string plotPath = Path.Combine(dataDir, "plot.png");
                plt.SavePng(plotPath, 600, 400);
                Console.WriteLine($"График сохранён в {plotPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при сохранении PNG: {ex.Message}");
            }

            Console.WriteLine(AsciiPlot.Render(result));
        }
    }
}