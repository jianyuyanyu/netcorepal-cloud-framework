using Xunit;
using System.Linq;
using System;

namespace NetCorePal.Extensions.CodeAnalysis.UnitTests.SourceGenerators;

public class EntityMethodMetadataGeneratorTests
{
    [Fact]
    public void Should_Generate_EntityMethodMetadataAttribute()
    {
        var assembly = typeof(EntityMethodMetadataGeneratorTests).Assembly;
        var attrs = assembly.GetCustomAttributes(typeof(NetCorePal.Extensions.CodeAnalysis.Attributes.EntityMethodMetadataAttribute), false)
            .Cast<NetCorePal.Extensions.CodeAnalysis.Attributes.EntityMethodMetadataAttribute>()
            .ToList();
        Assert.NotNull(attrs);
        
        // 测试实例方法 - 现在包含参数签名
        Assert.Contains(attrs, a => a.EntityType == "NetCorePal.Extensions.CodeAnalysis.UnitTests.TestClasses.TestAggregateRoot"
            && a.MethodName == "ChangeName(string)"
            && a.EventTypes.SequenceEqual(new[]{"NetCorePal.Extensions.CodeAnalysis.UnitTests.TestClasses.TestAggregateRootNameChangedDomainEvent"})
            && a.CalledEntityMethods.Length == 0);
            
        // 测试私有方法 - 现在包含参数签名
        Assert.Contains(attrs, a => a.EntityType == "NetCorePal.Extensions.CodeAnalysis.UnitTests.TestClasses.TestAggregateRoot"
            && a.MethodName == "PrivateMethod()"
            && a.EventTypes.SequenceEqual(new[]{"NetCorePal.Extensions.CodeAnalysis.UnitTests.TestClasses.TestPrivateMethodDomainEvent"})
            && a.CalledEntityMethods.SequenceEqual(new[]{"NetCorePal.Extensions.CodeAnalysis.UnitTests.TestClasses.TestEntity.ChangeTestEntityName(string)"}));
            
        // 测试实体方法 - 现在包含参数签名
        Assert.Contains(attrs, a => a.EntityType == "NetCorePal.Extensions.CodeAnalysis.UnitTests.TestClasses.TestEntity"
            && a.MethodName == "ChangeTestEntityName(string)"
            && a.EventTypes.SequenceEqual(new[]{"NetCorePal.Extensions.CodeAnalysis.UnitTests.TestClasses.TestEntityNameChangedDomainEvent"})
            && a.CalledEntityMethods.Length == 0);
            
        // 测试构造函数 - 现在包含参数签名
        Assert.Contains(attrs, a => a.EntityType == "NetCorePal.Extensions.CodeAnalysis.UnitTests.TestClasses.TestAggregateRoot"
            && a.MethodName.StartsWith(".ctor(")
            && a.EventTypes.Length >= 0
            && a.CalledEntityMethods.Length >= 0);
            
        // 测试静态方法 - 现在包含参数签名
        Assert.Contains(attrs, a => a.EntityType == "NetCorePal.Extensions.CodeAnalysis.UnitTests.TestClasses.TestAggregateRoot"
            && a.MethodName == "Create(TestAggregateRootId)"
            && a.EventTypes.Length == 0
            && a.CalledEntityMethods.Length == 0);
    }
    
    [Fact]
    public void Should_Generate_Unique_Metadata_For_Overloaded_Methods()
    {
        var assembly = typeof(EntityMethodMetadataGeneratorTests).Assembly;
        var attrs = assembly.GetCustomAttributes(typeof(NetCorePal.Extensions.CodeAnalysis.Attributes.EntityMethodMetadataAttribute), false)
            .Cast<NetCorePal.Extensions.CodeAnalysis.Attributes.EntityMethodMetadataAttribute>()
            .Where(a => a.EntityType == "NetCorePal.Extensions.CodeAnalysis.UnitTests.TestClasses.OverloadedEntity")
            .ToList();
        
        // 验证方法重载生成了不同的元数据
        // 构造函数重载
        Assert.Contains(attrs, a => a.MethodName == ".ctor(OverloadedEntityId)");
        Assert.Contains(attrs, a => a.MethodName == ".ctor(OverloadedEntityId,string)");
        Assert.Contains(attrs, a => a.MethodName == ".ctor(OverloadedEntityId,string,int)");
        
        // Create 静态方法重载
        Assert.Contains(attrs, a => a.MethodName == "Create()");
        Assert.Contains(attrs, a => a.MethodName == "Create(string)");
        Assert.Contains(attrs, a => a.MethodName == "Create(string,int)");
        
        // Update 实例方法重载
        Assert.Contains(attrs, a => a.MethodName == "Update()");
        Assert.Contains(attrs, a => a.MethodName == "Update(string)");
        Assert.Contains(attrs, a => a.MethodName == "Update(string,int)");
        
        // 验证没有重复的键
        var methodNames = attrs.Select(a => a.MethodName).ToList();
        Assert.Equal(methodNames.Count, methodNames.Distinct().Count());
    }
}
