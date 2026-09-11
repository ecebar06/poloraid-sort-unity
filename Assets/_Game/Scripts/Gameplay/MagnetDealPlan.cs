using System.Collections.Generic;
using System.Linq;

// Deal whole dependency-ready categories. A completed set frees four magnets
// before producing one output; do not reserve empty slots for future sets.
// Completed finals release their holder; waiting outputs are consumed first.
public static class MagnetDealPlan
{
    public const int Capacity = 16;

    public static List<string> SelectNext(LevelData level, HashSet<string> done,
        HashSet<string> active, int looseCount, int retained, int limit)
    {
        int occupied = looseCount;
        var selected = new List<string>();
        foreach (var category in level.categories.Where(c => !done.Contains(c.id) && !active.Contains(c.id) &&
            level.categories.Where(s => s.transformsOnComplete && s.transformResultCategoryId == c.id).All(s => done.Contains(s.id)))
            .OrderByDescending(c => level.categories.Count(s => s.transformsOnComplete && s.transformResultCategoryId == c.id)))
        {
            int cost = 4 - level.categories.Count(s => s.transformsOnComplete && s.transformResultCategoryId == category.id);
            if (selected.Count >= limit || active.Count + retained + selected.Count >= 4 || occupied + cost > Capacity) continue;
            selected.Add(category.id);
            occupied += cost;
        }
        return selected;
    }

    public static bool TryBuild(LevelData level, out List<List<string>> waves)
    {
        waves = new List<List<string>>();
        var done = new HashSet<string>();
        var active = new HashSet<string>();
        int loose = 0, retained = 0;
        while (done.Count < level.categories.Count)
        {
            var added = SelectNext(level, done, active, loose, retained, done.Count == 0 ? 4 : 1);
            if (added.Count > 0) waves.Add(added);
            foreach (string id in added)
            {
                active.Add(id);
                loose += 4 - level.categories.Count(s => s.transformsOnComplete && s.transformResultCategoryId == id);
            }
            if (active.Count == 0) return false;
            var category = level.categories.First(c => active.Contains(c.id));
            active.Remove(category.id);
            done.Add(category.id);
            loose -= 4;
            if (category.transformsOnComplete) loose++;
            else if (level.categories.Count - done.Count < 4) retained++;
        }
        return true;
    }
}
