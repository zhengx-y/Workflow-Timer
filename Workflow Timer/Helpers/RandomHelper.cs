namespace Workflow_Timer.Helpers
{
    public static class RandomHelper
    {
        public static int NextIndex(int count, int previousIndex = -1)
        {
            if (count <= 0)
                throw new global::System.ArgumentOutOfRangeException(nameof(count));
            if (count == 1)
                return 0;
            if (previousIndex < 0 || previousIndex >= count)
                return global::System.Random.Shared.Next(count);

            int index = global::System.Random.Shared.Next(count - 1);
            return index >= previousIndex ? index + 1 : index;
        }
    }
}
