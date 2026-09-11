using System;
using System.Collections.Generic;
using System.Linq;

public static class MixedMagnetStock
{
    private static string Key(LevelWordEntry e) => e.categoryId + ":" + e.word.text.ToLowerInvariant();
    public static List<LevelWordEntry> Words(LevelData level)
    {
        var generated = new HashSet<string>(level.categories.Where(c => c.transformsOnComplete)
            .Select(c => c.transformResultCategoryId + ":" + c.transformResult.text.ToLowerInvariant()));
        return level.categories.SelectMany(c => c.words.Select(w => new LevelWordEntry { categoryId = c.id, word = w }))
            .Where(e => !generated.Contains(Key(e))).ToList();
    }

    // Exhaustive completion-order check, with a conservative partial-holder
    // condition: incomplete visible categories cannot fill all usable holders.
    public static bool IsSafe(LevelData level, List<LevelWordEntry> stock)
    {
        int n = level.categories.Count;
        if (n == 0 || n > 20 || stock == null) return false;
        if (!Words(level).Select(Key).OrderBy(k => k).SequenceEqual(stock.Select(Key).OrderBy(k => k))) return false;
        var ids = level.categories.Select(c => c.id).ToList();
        var order = stock.Select(e => ids.IndexOf(e.categoryId)).ToArray();
        var memo = new Dictionary<long, bool>();
        int visits = 0;
        bool Visit(int mask, int kept, int count, int transforms)
        {
            if (count == n) return true;
            long key = ((long)mask << 3) | (uint)kept;
            if (memo.TryGetValue(key, out bool cached)) return cached;
            if (++visits > 20000) return false;
            int[] available = new int[n];
            int cursor = Math.Min(order.Length, 16 + count * 4 - transforms);
            for (int i = 0; i < cursor; i++) available[order[i]]++;
            for (int i = 0; i < n; i++)
                if ((mask & (1 << i)) != 0 && level.categories[i].transformsOnComplete)
                {
                    int target = ids.IndexOf(level.categories[i].transformResultCategoryId);
                    if (target < 0) return false;
                    available[target]++;
                }
            int incomplete = 0;
            var ready = new List<int>();
            for (int i = 0; i < n; i++) if ((mask & (1 << i)) == 0)
            {
                if (available[i] == 4) ready.Add(i);
                else if (available[i] > 0) incomplete++;
            }
            if (ready.Count == 0 || incomplete >= 4 - kept) return memo[key] = false;
            foreach (int i in ready)
            {
                bool transform = level.categories[i].transformsOnComplete;
                bool keepFinal = !transform && cursor >= stock.Count && n - count - 1 <= 4 - kept - 1;
                if (!Visit(mask | (1 << i), kept + (keepFinal ? 1 : 0),
                    count + 1, transforms + (transform ? 1 : 0))) return memo[key] = false;
            }
            return memo[key] = true;
        }
        return Visit(0, 0, 0, 0);
    }

    private static int Score(List<LevelWordEntry> stock)
    {
        int score = 0;
        for (int i = 16; i < stock.Count; i++)
        {
            if (stock[i].categoryId != stock[i - 1].categoryId) score += 2;
            if (i >= 18 && stock.Skip(i - 2).Take(3).Select(e => e.categoryId).Distinct().Count() > 1) score++;
        }
        return score;
    }

    public static List<LevelWordEntry> Generate(LevelData level)
    {
        if (level.categories.Count > 20) throw new InvalidOperationException("Mixed stock supports up to 20 categories per level.");
        var random = new Random();
        var words = Words(level);
        List<LevelWordEntry> stock = null;
        for (int attempt = 0; attempt < 100 && stock == null; attempt++)
        {
            var pending = level.categories.ToList();
            var done = new HashSet<string>();
            var candidate = new List<LevelWordEntry>();
            while (pending.Count > 0)
            {
                var ready = pending.Where(c => level.categories.Where(s => s.transformsOnComplete &&
                    s.transformResultCategoryId == c.id).All(s => done.Contains(s.id))).ToList();
                if (ready.Count == 0) throw new InvalidOperationException("Transformation cycle.");
                var category = ready[attempt == 0 ? 0 : random.Next(ready.Count)];
                pending.Remove(category);
                done.Add(category.id);
                candidate.AddRange(words.Where(e => e.categoryId == category.id));
            }
            if (IsSafe(level, candidate)) stock = candidate;
        }
        if (stock == null) throw new InvalidOperationException("No verified mixed stock found. Adjust the category graph.");
        int score = Score(stock);
        for (int attempt = 0; attempt < 160; attempt++)
        {
            int a = random.Next(stock.Count), b = random.Next(stock.Count);
            if (a == b || stock[a].categoryId == stock[b].categoryId) continue;
            (stock[a], stock[b]) = (stock[b], stock[a]);
            int next = Score(stock);
            if (next >= score && IsSafe(level, stock)) score = next;
            else (stock[a], stock[b]) = (stock[b], stock[a]);
        }
        return stock;
    }
}
