using IdGen;

namespace Extensions
{
    public class RepositoryHelper
    {
        private static IdGenerator gen = new IdGen.IdGenerator(System.Environment.MachineName.GetHashCode() & (1024 - 1));
        public static string NewId()
        {
            return gen.CreateId().ToString("x16") + NanoidDotNet.Nanoid.Generate("0123456789abcdef", 16);
        }

    }
}
