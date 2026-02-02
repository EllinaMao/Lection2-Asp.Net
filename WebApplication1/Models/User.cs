namespace WebApplication1.Models
{
    record User(int Id, string Name, int Age)
    {
        public User(string Name, int Age) : this(0, Name, Age)
        {

        }
    }



}
