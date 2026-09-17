using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ConsoleApp1
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            string folder = @"D:\PROG\ConsoleApp1\ConsoleApp1\test"; //путь к папке с тестовыми файлами

            GenerateFiles(folder); //генерация тестовых файлов

            //ЭТАП 1: Успешн выполн всех трех операций (таймаут 10 секунд)
            Console.WriteLine("ЭТАП 1: Запуск всех методов (с запасом времени)");

            using var ctsSuccess = new CancellationTokenSource();
            ctsSuccess.CancelAfter(30000);//источник токена

            //Синхронный метод
            var sw = Stopwatch.StartNew();
            string syncWord = "";
            for (int i=0;i<100;i++)
            {
                syncWord = FindLongestWordSync(folder);
            }
            sw.Stop();
            Console.WriteLine($"Синхронный (100 раз): {sw.ElapsedMilliseconds} мс, слово \"{syncWord}\"");

            //Асинхронный последовательный метод
            sw.Restart();
            try
            {
                string seqWord = "";
                for (int i=0;i<100;i++)
                {
                    seqWord = await FindLongestWordSequentialAsync(folder, ctsSuccess.Token);
                }
                sw.Stop();
                Console.WriteLine($"Асинхронный последовательный (100 раз): {sw.ElapsedMilliseconds} мс, слово \"{seqWord}\"");
            }
            catch (OperationCanceledException)
            {
                sw.Stop();
                Console.WriteLine($"Асинхронный последовательный: Отмена на этапе 1 ({sw.ElapsedMilliseconds} мс)");
            }

            //Асинхронный параллельный метод (Task.WhenAll)
            sw.Restart();
            try
            {
                string parWord = "";
                for (int i = 0; i < 100; i++)
                {
                    parWord = await FindLongestWordParallelAsync(folder, ctsSuccess.Token);//cts..Token - флажок, передаётся в методы
                }
                sw.Stop();
                Console.WriteLine($"Асинхронный параллельный (100 раз): {sw.ElapsedMilliseconds} мс, слово \"{parWord}\"");
            }
            catch (OperationCanceledException)
            {
                sw.Stop();
                Console.WriteLine($"Асинхронный параллельный: Отмена на этапе 1 ({sw.ElapsedMilliseconds} мс)");
            }


            //ЭТАП 2: повторн запуск операции с мгновенной отменой по таймауту

            Console.WriteLine("\nЭТАП 2: Повторный запуск параллельного метода с отменой");

            using var ctsCancel = new CancellationTokenSource();
            //таймаут - 1 миллисекунда, чтобы метод гарантированно отменился
            ctsCancel.CancelAfter(1);

            sw.Restart();
            try
            {
                string canceledWord = await FindLongestWordParallelAsync(folder, ctsCancel.Token); //новый быстрый токен
                sw.Stop();
                Console.WriteLine($"Повторный запуск: Успешно выполнился за {sw.ElapsedMilliseconds} мс (слово: \"{canceledWord}\")");
            }
            catch (OperationCanceledException)
            {
                sw.Stop();
                Console.WriteLine($"Повторный запуск: ОПЕРАЦИЯ ОТМЕНЕНА ПО ТАЙМАУТУ! (прошло {sw.ElapsedMilliseconds} мс)");
            }
        }

        //СИНХРОННЫЙ МЕТОД
        static string FindLongestWordSync(string folder)
        {
            string overallLongest = "";
            foreach (var file in Directory.GetFiles(folder))
            {
                string content = File.ReadAllText(file);
                string longestInFile = FindLongestWord(content);

                if (longestInFile.Length > overallLongest.Length)
                    overallLongest = longestInFile;
            }
            return overallLongest;
        }

        //АСИНХРОННЫЙ ПОСЛЕДОВАТЕЛЬНЫЙ МЕТОД
        static async Task<string> FindLongestWordSequentialAsync(string folder, CancellationToken token)//токен отмены
        {
            string overallLongest = "";
            foreach (var file in Directory.GetFiles(folder))
            {
                token.ThrowIfCancellationRequested(); 

                string content = await File.ReadAllTextAsync(file, token); //передача токена внутрь

                //await Task.Delay(10, token); //имитация задержки чтения для надежности срабатывания таймаутов

                //string content = await
                    //File.ReadAllTextAsync(file, token);
                string longestInFile = FindLongestWord(content);

                if (longestInFile.Length > overallLongest.Length)
                    overallLongest = longestInFile;
            }
            return overallLongest;
        }

        //АСИНХРОННЫЙ ПАРАЛЛЕЛЬНЫЙ МЕТОД (Task.WhenAll)
        static async Task<string> FindLongestWordParallelAsync(string folder, CancellationToken token)
        {
            string[] files = Directory.GetFiles(folder);
            List<Task<string>> tasks = new List<Task<string>>();

            foreach (var file in files)
            {
                tasks.Add(File.ReadAllTextAsync(file, token)); //Метод-обертка, чтобы добавить задержку к каждой параллельной задаче
            }

            string[] contents = await Task.WhenAll(tasks); //ожидает считывание всех файлов вместе

            string overallLongest = "";
            foreach (string content in contents)
            {
                string longestInFile = FindLongestWord(content);
                if (longestInFile.Length > overallLongest.Length)
                    overallLongest = longestInFile;
            }
            return overallLongest;
        }

        //вспомогательный асинхронный метод для имитации задержки при параллельном чтении

        //static async Task<string> ReadFileWithDelayAsync(string path, CancellationToken token)
        //{
            //await Task.Delay(10, token);
            //return await File.ReadAllTextAsync(path, token);
        //}

        //ВСПОМОГАТЕЛЬНЫЙ МЕТОД: Поиск самого длинного слова в одной строке текста
        static string FindLongestWord(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";

            string[] words = text.Split(new[] { ' ', '\n', '\r', '\t', '.', ',', ';', '!', '?' },
                                        StringSplitOptions.RemoveEmptyEntries);

            string longest = "";
            foreach (var word in words)
            {
                if (word.Length > longest.Length)
                    longest = word;
            }
            return longest;
        }

        //ГЕНЕРАЦИЯ ФАЙЛОВ
        static void GenerateFiles(string folder)
        {
            Directory.CreateDirectory(folder);
            long[] sizes = new long[10];
            sizes[0] = 1024;
            for (int i = 1; i < sizes.Length; i++)
            {
                sizes[i] = sizes[i - 1] * 2;
            }

            string pattern = "Wish i could pay off my debts. ";

            for (int i = 0; i < sizes.Length; i++)
            {
                string path = Path.Combine(folder, $"file_{i}.txt");
                using var writer = new StreamWriter(path);
                long written = 0;
                while (written < sizes[i])
                {
                    int toWrite = (int)Math.Min(pattern.Length, sizes[i] - written);
                    writer.Write(pattern.ToCharArray(), 0, toWrite);
                    written += toWrite;
                }
            }
            Console.WriteLine($"Создано {sizes.Length} файлов в папке {folder}.\n");
        }
    }
}
