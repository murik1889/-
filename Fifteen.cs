using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Fifteen
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    // ---------- Логика ----------
    static class Solver
    {
        public static readonly int[] Goal = { 1, 2, 3, 4, 5, 6, 7, 8, 0 };

        public static bool IsSolvable(int[] s)
        {
            var arr = s.Where(x => x != 0).ToArray();
            int inv = 0;
            for (int i = 0; i < arr.Length; i++)
                for (int j = i + 1; j < arr.Length; j++)
                    if (arr[i] > arr[j]) inv++;
            return inv % 2 == 0;
        }

        public static List<(int[] state, string desc)> Neighbors(int[] s)
        {
            var res = new List<(int[], string)>();
            int idx = Array.IndexOf(s, 0);
            int r = idx / 3, c = idx % 3;

            var moves = new (int dr, int dc, string tileDir)[]
            {
                (-1, 0, "вниз"),
                ( 1, 0, "вверх"),
                ( 0,-1, "вправо"),
                ( 0, 1, "влево"),
            };

            foreach (var m in moves)
            {
                int nr = r + m.dr, nc = c + m.dc;
                if (nr < 0 || nr > 2 || nc < 0 || nc > 2) continue;
                int nidx = nr * 3 + nc;
                int tile = s[nidx];
                var ns = (int[])s.Clone();
                (ns[idx], ns[nidx]) = (ns[nidx], ns[idx]);
                res.Add((ns, $"плитка {tile} → {m.tileDir}"));
            }
            return res;
        }

        static int H(int[] s)
        {
            int d = 0;
            for (int i = 0; i < 9; i++)
            {
                int v = s[i];
                if (v == 0) continue;
                int gr = (v - 1) / 3, gc = (v - 1) % 3;
                int r = i / 3, c = i % 3;
                d += Math.Abs(r - gr) + Math.Abs(c - gc);
            }
            return d;
        }

        static string Key(int[] s) => string.Join(",", s);

        public static List<(int[] state, string desc)> Solve(int[] start)
        {
            if (start.SequenceEqual(Goal)) return new();
            if (!IsSolvable(start)) return null;

            var open = new PriorityQueue<(int[] s, List<(int[], string)> path), int>();
            var best = new Dictionary<string, int> { [Key(start)] = 0 };
            open.Enqueue((start, new()), H(start));

            while (open.Count > 0)
            {
                var (cur, path) = open.Dequeue();
                int g = path.Count;
                if (cur.SequenceEqual(Goal)) return path;

                foreach (var (ns, desc) in Neighbors(cur))
                {
                    int ng = g + 1;
                    var k = Key(ns);
                    if (!best.ContainsKey(k) || ng < best[k])
                    {
                        best[k] = ng;
                        var np = new List<(int[], string)>(path) { (ns, desc) };
                        open.Enqueue((ns, np), ng + H(ns));
                    }
                }
            }
            return null;
        }
    }

    // ---------- GUI ----------
    class MainForm : Form
    {
        const int TileSize = 92;
        const int Gap = 10;

        int[] state = (int[])Solver.Goal.Clone();
        List<(int[] state, string desc)> solution = new();
        int step = 0;
        int[] initial = (int[])Solver.Goal.Clone();

        Panel board;
        Button[] tiles;
        TextBox guideBox;
        Label statusLabel;

        int dragSrc = -1;
        Point dragStart;
        bool dragActive = false;

        public MainForm()
        {
            Text = "Пятнашки 3×3";
            BackColor = Color.FromArgb(17, 17, 27);
            ClientSize = new Size(760, 720);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            DoubleBuffered = true;

            BuildUI();
            Redraw();
        }

        void BuildUI()
        {
            // Заголовок
            var header = new Label
            {
                Text = "🧩 Пятнашки 3×3",
                ForeColor = Color.FromArgb(205, 214, 244),
                Font = new Font("Segoe UI", 18, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(20, 16)
            };
            Controls.Add(header);

            var sub = new Label
            {
                Text = "Перетаскивай плитки · ПКМ — очистить · клик — ввести число",
                ForeColor = Color.FromArgb(108, 112, 134),
                Font = new Font("Segoe UI", 9),
                AutoSize = true,
                Location = new Point(22, 52)
            };
            Controls.Add(sub);

            // Поле
            int size = TileSize * 3 + Gap * 4;
            board = new Panel
            {
                Location = new Point(20, 90),
                Size = new Size(size, size),
                BackColor = Color.FromArgb(30, 30, 46),
                BorderStyle = BorderStyle.FixedSingle
            };
            Controls.Add(board);

            tiles = new Button[9];
            for (int i = 0; i < 9; i++)
            {
                int r = i / 3, c = i % 3;
                var b = new Button
                {
                    Size = new Size(TileSize, TileSize),
                    Location = new Point(Gap + c * (TileSize + Gap),
                                         Gap + r * (TileSize + Gap)),
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 26, FontStyle.Bold),
                    BackColor = Color.FromArgb(49, 50, 68),
                    ForeColor = Color.FromArgb(205, 214, 244),
                    Text = "",
                    TabStop = false
                };
                b.FlatAppearance.BorderSize = 2;
                b.FlatAppearance.BorderColor = Color.FromArgb(88, 91, 112);

                int idx = i;
                b.MouseDown += (s, e) => OnPress(idx, e);
                b.MouseMove += (s, e) => OnMotion(idx, e);
                b.MouseUp   += (s, e) => OnRelease(idx, e);
                b.MouseClick += (s, e) =>
                {
                    if (e.Button == MouseButtons.Right) OnRightClick(idx);
                };

                tiles[i] = b;
                board.Controls.Add(b);
            }

            // Кнопки управления
            int bx = board.Right + 20;
            int by = 90;

            MakeBtn("🎲  Случайно", bx, by,      Color.FromArgb(249, 226, 175), Randomize);
            MakeBtn("🧠  Решить",   bx, by + 50, Color.FromArgb(166, 227, 161), SolveIt);
            MakeBtn("🎯  Сброс",    bx, by + 100,Color.FromArgb(137, 180, 250), ResetGoal);

            by += 170;
            var lbl = new Label
            {
                Text = "Прохождение",
                ForeColor = Color.FromArgb(205, 214, 244),
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(bx, by)
            };
            Controls.Add(lbl);

            by += 26;
            MakeBtn("◀", bx,      by, Color.FromArgb(245, 194, 231), PrevStep, 40);
            MakeBtn("▶", bx + 48, by, Color.FromArgb(245, 194, 231), NextStep, 40);
            MakeBtn("▶▶", bx + 96,by, Color.FromArgb(245, 194, 231), PlayAll, 55);

            // Статус
            statusLabel = new Label
            {
                Text = "Готов к работе",
                ForeColor = Color.FromArgb(108, 112, 134),
                Font = new Font("Segoe UI", 9),
                Location = new Point(bx, by + 60),
                Size = new Size(180, 60)
            };
            Controls.Add(statusLabel);

            // Гайд
            guideBox = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Location = new Point(20, board.Bottom + 20),
                Size = new Size(720, ClientSize.Height - board.Bottom - 40),
                BackColor = Color.FromArgb(24, 24, 37),
                ForeColor = Color.FromArgb(205, 214, 244),
                Font = new Font("Consolas", 10),
                BorderStyle = BorderStyle.FixedSingle,
                ReadOnly = true,
                Text = "Здесь появится гайд после нажатия «🧠 Решить».\r\n\r\n" +
                       "Быстрый старт:\r\n" +
                       "  • Перетаскивай плитки мышью, чтобы задать раскладку.\r\n" +
                       "  • Или «🎲 Случайно» → «🧠 Решить».\r\n" +
                       "  • Кнопки ◀ ▶ ▶▶ — пошаговое прохождение.\r\n"
            };
            Controls.Add(guideBox);
        }

        Button MakeBtn(string text, int x, int y, Color color, Action action, int width = 180)
        {
            var b = new Button
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(width, 42),
                BackColor = color,
                ForeColor = Color.FromArgb(17, 17, 27),
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderSize = 0;
            b.Click += (s, e) => action();
            Controls.Add(b);
            return b;
        }

        void Redraw()
        {
            for (int i = 0; i < 9; i++)
            {
                int v = state[i];
                var b = tiles[i];
                if (v == 0)
                {
                    b.Text = "";
                    b.BackColor = Color.FromArgb(24, 24, 37);
                }
                else
                {
                    b.Text = v.ToString();
                    bool inPlace = (i == v - 1);
                    b.BackColor = inPlace ? Color.FromArgb(166, 227, 161)
                                          : Color.FromArgb(49, 50, 68);
                    b.ForeColor = inPlace ? Color.FromArgb(17, 17, 27)
                                          : Color.FromArgb(205, 214, 244);
                }
            }
        }

        // ---------- События ----------
        void OnPress(int idx, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            dragSrc = idx;
            dragStart = e.Location;
            dragActive = false;
        }

        void OnMotion(int idx, MouseEventArgs e)
        {
            if (dragSrc < 0 || e.Button != MouseButtons.Left) return;
            int dx = e.X - dragStart.X;
            int dy = e.Y - dragStart.Y;
            if (Math.Abs(dx) > 6 || Math.Abs(dy) > 6) dragActive = true;
        }

        void OnRelease(int idx, MouseEventArgs e)
        {
            int src = dragSrc;
            dragSrc = -1;
            if (e.Button != MouseButtons.Left || src < 0) return;

            if (dragActive)
            {
                // Определяем, над какой плиткой отпустили
                Point screenPt = tiles[src].PointToScreen(e.Location);
                int dst = -1;
                for (int i = 0; i < 9; i++)
                {
                    var r = tiles[i].RectangleToScreen(tiles[i].ClientRectangle);
                    if (r.Contains(screenPt)) { dst = i; break; }
                }
                if (dst >= 0 && dst != src) SwapCells(src, dst);
            }
            else
            {
                PromptNumber(src);
            }
        }

        void OnRightClick(int idx)
        {
            if (state[idx] == 0) return;
            int oldZero = Array.IndexOf(state, 0);
            (state[oldZero], state[idx]) = (state[idx], state[oldZero]);
            Redraw();
            AfterEdit($"Клетка «{PosName(idx)}» очищена.");
        }

        void SwapCells(int a, int b)
        {
            (state[a], state[b]) = (state[b], state[a]);
            Redraw();
            AfterEdit($"↔ {PosName(a)} ⇄ {PosName(b)}");
        }

        void PromptNumber(int idx)
        {
            int cur = state[idx];
            string curTxt = cur == 0 ? "пусто" : cur.ToString();

            using var dlg = new NumberDialog(
                $"Клетка: {PosName(idx)}\nСейчас: {curTxt}\n\nВведи 1–8 (0 = очистить):",
                cur);
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            int val = dlg.Value;
            if (val == 0)
            {
                int oldZero = Array.IndexOf(state, 0);
                if (idx != oldZero)
                    (state[oldZero], state[idx]) = (state[idx], state[oldZero]);
                Redraw();
                AfterEdit($"Клетка «{PosName(idx)}» очищена.");
                return;
            }
            if (val == cur) return;

            int other = Array.IndexOf(state, val);
            if (other >= 0)
            {
                (state[other], state[idx]) = (state[idx], state[other]);
                Redraw();
                AfterEdit($"↔ {val}: {PosName(other)} ⇄ {PosName(idx)}");
            }
            else
            {
                int zp = Array.IndexOf(state, 0);
                (state[zp], state[idx]) = (state[idx], state[zp]);
                state[idx] = val;
                Redraw();
                AfterEdit($"«{PosName(idx)}» = {val}");
            }
        }

        string PosName(int i) => new[] { "В-Л", "В-Ц", "В-П", "С-Л", "🎯", "С-П", "Н-Л", "Н-Ц", "Н-П" }[i];

        void AfterEdit(string msg)
        {
            solution.Clear();
            step = 0;
            statusLabel.Text = msg;
        }

        // ---------- Кнопки ----------
        void ResetGoal()
        {
            state = (int[])Solver.Goal.Clone();
            solution.Clear();
            step = 0;
            Redraw();
            statusLabel.Text = "Поле сброшено в цель.";
            guideBox.Text = "Поле в целевой раскладке.\r\n";
        }

        void Randomize()
        {
            var rnd = new Random();
            int[] arr;
            do
            {
                arr = Enumerable.Range(0, 9).OrderBy(_ => rnd.Next()).ToArray();
            } while (!Solver.IsSolvable(arr) || arr.SequenceEqual(Solver.Goal));

            state = arr;
            solution.Clear();
            step = 0;
            Redraw();
            statusLabel.Text = "Случайная решаемая раскладка.";
            guideBox.Text = "Случайная раскладка. Жми «🧠 Решить».\r\n";
        }

        void SolveIt()
        {
            if (!state.OrderBy(x => x).SequenceEqual(Enumerable.Range(0, 9)))
            {
                MessageBox.Show("На поле должны быть ровно 0..8 без повторов.", "Ошибка");
                return;
            }
            if (!Solver.IsSolvable(state))
            {
                guideBox.Text = "❌ Нерешаемая раскладка.\r\n\r\n" +
                                "Для 3×3 нужно чётное число инверсий. Поменяй две любые плитки.\r\n";
                statusLabel.Text = "Нерешаемая раскладка.";
                return;
            }
            var sol = Solver.Solve(state);
            if (sol == null)
            {
                MessageBox.Show("Решение не найдено.", "Ошибка");
                return;
            }
            initial = (int[])state.Clone();
            solution = sol;
            step = 0;
            statusLabel.Text = $"Решение: {sol.Count} ходов.";
            ShowGuide();
        }

        void ShowGuide()
        {
            if (solution.Count == 0)
            {
                guideBox.Text = "✅ Уже решено!\r\n";
                return;
            }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"📋 ГАЙД: {solution.Count} ходов");
            sb.AppendLine(new string('─', 60));
            sb.AppendLine();
            sb.AppendLine("Старт:");
            sb.AppendLine(Fmt(state));
            sb.AppendLine();
            int i = 1;
            foreach (var (ns, desc) in solution)
            {
                sb.AppendLine($"{i,2}. {desc}");
                sb.AppendLine(Fmt(ns));
                sb.AppendLine();
                i++;
            }
            sb.AppendLine("💡 Стратегия вручную:");
            sb.AppendLine("  • Собери верхний ряд (1,2,3) — больше его не трогай.");
            sb.AppendLine("  • Затем левый столбец.");
            sb.AppendLine("  • Оставшиеся 2×2 — крути по часовой стрелке.");
            guideBox.Text = sb.ToString();
            guideBox.SelectionStart = 0;
        }

        string Fmt(int[] s)
        {
            var sb = new System.Text.StringBuilder();
            for (int r = 0; r < 3; r++)
            {
                sb.Append("   ");
                for (int c = 0; c < 3; c++)
                {
                    int v = s[r * 3 + c];
                    sb.Append(v == 0 ? "·" : v.ToString());
                    sb.Append("  ");
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }

        void NextStep()
        {
            if (solution.Count == 0) return;
            if (step >= solution.Count)
            {
                statusLabel.Text = "🎉 Готово!";
                return;
            }
            var (ns, desc) = solution[step];
            state = (int[])ns.Clone();
            step++;
            Redraw();
            statusLabel.Text = $"Шаг {step}/{solution.Count}: {desc}";
        }

        void PrevStep()
        {
            if (step == 0 || solution.Count == 0) return;
            step--;
            state = step == 0
                ? (int[])initial.Clone()
                : (int[])solution[step - 1].state.Clone();
            Redraw();
            statusLabel.Text = $"Откат к шагу {step}";
        }

        void PlayAll()
        {
            if (solution.Count == 0 || step >= solution.Count) return;

            var timer = new Timer { Interval = 400 };
            timer.Tick += (s, e) =>
            {
                if (step >= solution.Count)
                {
                    timer.Stop();
                    timer.Dispose();
                    statusLabel.Text = "🎉 Готово!";
                    return;
                }
                var (ns, desc) = solution[step];
                state = (int[])ns.Clone();
                step++;
                Redraw();
                statusLabel.Text = $"Шаг {step}/{solution.Count}: {desc}";
            };
            timer.Start();
        }
    }

    // Простой диалог ввода числа
    class NumberDialog : Form
    {
        NumericUpDown nud;
        public int Value => (int)nud.Value;

        public NumberDialog(string prompt, int current)
        {
            Text = "Ввод числа";
            ClientSize = new Size(280, 180);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(30, 30, 46);
            ForeColor = Color.FromArgb(205, 214, 244);
            Font = new Font("Segoe UI", 10);

            Controls.Add(new Label
            {
                Text = prompt,
                Location = new Point(14, 12),
                Size = new Size(250, 60),
                ForeColor = ForeColor,
                Font = Font
            });

            nud = new NumericUpDown
            {
                Minimum = 0,
                Maximum = 8,
                Value = current,
                Location = new Point(14, 84),
                Size = new Size(80, 26),
                Font = new Font("Segoe UI", 12),
                BackColor = Color.FromArgb(24, 24, 37),
                ForeColor = Color.FromArgb(205, 214, 244)
            };
            Controls.Add(nud);

            var ok = new Button
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                Location = new Point(100, 120),
                Size = new Size(80, 32),
                BackColor = Color.FromArgb(166, 227, 161),
                ForeColor = Color.FromArgb(17, 17, 27),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };
            ok.FlatAppearance.BorderSize = 0;
            Controls.Add(ok);

            var cancel = new Button
            {
                Text = "Отмена",
                DialogResult = DialogResult.Cancel,
                Location = new Point(184, 120),
                Size = new Size(80, 32),
                BackColor = Color.FromArgb(69, 71, 90),
                ForeColor = Color.FromArgb(205, 214, 244),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };
            cancel.FlatAppearance.BorderSize = 0;
            Controls.Add(cancel);

            AcceptButton = ok;
            CancelButton = cancel;
        }
    }
}