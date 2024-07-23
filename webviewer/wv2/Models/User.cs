using Repositories;

namespace Models
{
    public class User : IEntity
    {
        public string Id { get; set; } = RepositoryHelper.NewId();
        public string Account { get; set; }
        public string Password { get; set; }
        public User(string account, string password)
        {
            this.Account = account;
            this.Password = password;
        }
    }
}
