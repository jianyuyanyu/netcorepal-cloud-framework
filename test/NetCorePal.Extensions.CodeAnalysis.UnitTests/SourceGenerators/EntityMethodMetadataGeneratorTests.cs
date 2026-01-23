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
        
        // 测试实例方法 - 不包含参数签名
        Assert.Contains(attrs, a => a.EntityType == "NetCorePal.Extensions.CodeAnalysis.UnitTests.TestClasses.TestAggregateRoot"
            && a.MethodName == "ChangeName"
            && a.EventTypes.SequenceEqual(new[]{"NetCorePal.Extensions.CodeAnalysis.UnitTests.TestClasses.TestAggregateRootNameChangedDomainEvent"})
            && a.CalledEntityMethods.Length == 0);
            
        // 测试私有方法 - 不包含参数签名
        Assert.Contains(attrs, a => a.EntityType == "NetCorePal.Extensions.CodeAnalysis.UnitTests.TestClasses.TestAggregateRoot"
            && a.MethodName == "PrivateMethod"
            && a.EventTypes.SequenceEqual(new[]{"NetCorePal.Extensions.CodeAnalysis.UnitTests.TestClasses.TestPrivateMethodDomainEvent"})
            && a.CalledEntityMethods.SequenceEqual(new[]{"NetCorePal.Extensions.CodeAnalysis.UnitTests.TestClasses.TestEntity.ChangeTestEntityName"}));
            
        // 测试实体方法 - 不包含参数签名
        Assert.Contains(attrs, a => a.EntityType == "NetCorePal.Extensions.CodeAnalysis.UnitTests.TestClasses.TestEntity"
            && a.MethodName == "ChangeTestEntityName"
            && a.EventTypes.SequenceEqual(new[]{"NetCorePal.Extensions.CodeAnalysis.UnitTests.TestClasses.TestEntityNameChangedDomainEvent"})
            && a.CalledEntityMethods.Length == 0);
            
        // 测试构造函数 - 不包含参数签名
        Assert.Contains(attrs, a => a.EntityType == "NetCorePal.Extensions.CodeAnalysis.UnitTests.TestClasses.TestAggregateRoot"
            && a.MethodName == ".ctor"
            && a.EventTypes.Length >= 0
            && a.CalledEntityMethods.Length >= 0);
            
        // 测试静态方法 - 不包含参数签名
        Assert.Contains(attrs, a => a.EntityType == "NetCorePal.Extensions.CodeAnalysis.UnitTests.TestClasses.TestAggregateRoot"
            && a.MethodName == "Create"
            && a.EventTypes.Length == 0
            && a.CalledEntityMethods.Length == 0);
    }
    
    [Fact]
    public void Should_Merge_Metadata_For_Overloaded_Methods()
    {
        var assembly = typeof(EntityMethodMetadataGeneratorTests).Assembly;
        var attrs = assembly.GetCustomAttributes(typeof(NetCorePal.Extensions.CodeAnalysis.Attributes.EntityMethodMetadataAttribute), false)
            .Cast<NetCorePal.Extensions.CodeAnalysis.Attributes.EntityMethodMetadataAttribute>()
            .Where(a => a.EntityType == "NetCorePal.Extensions.CodeAnalysis.UnitTests.TestClasses.OverloadedEntity")
            .ToList();
        
        // 验证方法重载被合并为单个元数据
        // 构造函数重载应该合并为一个 .ctor
        var ctorAttrs = attrs.Where(a => a.MethodName == ".ctor").ToList();
        Assert.Single(ctorAttrs); // 只应该有一个 .ctor 元数据
        
        // Create 静态方法重载应该合并为一个 Create
        var createAttrs = attrs.Where(a => a.MethodName == "Create").ToList();
        Assert.Single(createAttrs); // 只应该有一个 Create 元数据
        
        // Update 实例方法重载应该合并为一个 Update
        var updateAttrs = attrs.Where(a => a.MethodName == "Update").ToList();
        Assert.Single(updateAttrs); // 只应该有一个 Update 元数据
        
        // 总共应该有 3 个元数据项（.ctor, Create, Update）
        Assert.Equal(3, attrs.Count);
    }
}
