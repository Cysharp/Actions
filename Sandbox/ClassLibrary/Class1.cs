public class Foo
{
    private readonly string _name;
    private readonly int _age;

    public Foo(string name, int age)
    {
        _name = name;
        _age = age;
    }

    public void Bar()
    {
        Console.WriteLine($"name {_name}, age {_age}");
    }
}
