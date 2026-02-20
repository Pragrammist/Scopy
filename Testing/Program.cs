using ScopyRuntime;
using ScopySyntax;

namespace Testing;
// See https://aka.ms/new-console-template for more information


public static class Program
{
    public static void Main(string[] args)
    {

        TestClass.TestMethodVoid1();
    }
}



public static partial class TestClass
{
    
    
    public static int D {
        get
        {
            
            return 0;
        }
        set
        {
            
            
        } }




    public static void TestMethodVoid2()
    {
        
        if (true)
        {
            TestMethodVoid3();
        }

    }

    public static void TestMethodVoid3()
    {
        
        CurrentScope.Provide(new object());
        
    }
    
    
    public static void TestMethodVoid1()
    {

        TestMethodVoid2();
        
        for (int i = 0; i < 10; i++)
        {
            
             
            if (i == 1)
            {
                
            }
        }        
        
        // CurrentScope.Push();

    }
    
    public record TestValueReturn(string Value);
    
    
    
    public static TestValueReturn ProvideTestValue1()
    {
        return new TestValueReturn("");
    }
    
    
    public static TestValueReturn ProvideTestValue2()
    {
        return new TestValueReturn("");
    }
    
    
    public static void ProvideTestValue3()
    {
        return ;
    }
    
    
    
    


}

