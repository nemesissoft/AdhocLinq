using NUnit.Framework;

namespace AdhocLinq.Tests;

[TestFixture]
public class InternalTests
{
    [Test]
    public void ClassFactory_LoadTest()
    {
        //Arrange
        var rnd = new Random(1);

        var testPropertiesGroups = new[]
        {
            new[] {
                new DynamicProperty("String1", typeof( string )),
            },
            new[] {
                new DynamicProperty("String1", typeof( string )),
                new DynamicProperty("String2", typeof( string ))
            },
            new[] {
                new DynamicProperty("String1", typeof( string )),
                new DynamicProperty("Int1", typeof( int ))
            },
            new[] {
                new DynamicProperty("Int1", typeof( int )),
                new DynamicProperty("Int2", typeof( int ))
            },
            new[] {
                new DynamicProperty("String1", typeof( string )),
                new DynamicProperty("String2", typeof( string )),
                new DynamicProperty("String3", typeof( string )),
            },
        };

        void testActionSingle(int i)
        {
            ClassFactory.Instance.GetDynamicClass(testPropertiesGroups[0]);
        }

        void testActionMultiple(int i)
        {
            var testProperties = testPropertiesGroups[rnd.Next(0, testPropertiesGroups.Length)];

            ClassFactory.Instance.GetDynamicClass(testProperties);
        }

        //Act
        Parallel.For(0, 100000, testActionSingle);

        Parallel.For(0, 100000, testActionMultiple);

    }
}