namespace Repositories
{
    internal class RepositoryHelper
    {
        //private static readonly IdGenerator gen = new IdGen.IdGenerator(System.Environment.MachineName.GetHashCode() & (1024 - 1));
        public static string NewId()
        {
            //return gen.CreateId().ToString("x16") + NanoidDotNet.Nanoid.Generate("0123456789abcdef", 16);
            return Guid.NewGuid().ToString("N");
        }

    }
}
