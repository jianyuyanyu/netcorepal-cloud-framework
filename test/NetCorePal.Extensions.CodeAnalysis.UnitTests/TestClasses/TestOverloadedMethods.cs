using NetCorePal.Extensions.Domain;

namespace NetCorePal.Extensions.CodeAnalysis.UnitTests.TestClasses;

public partial record OverloadedEntityId : IGuidStronglyTypedId;

/// <summary>
/// 测试方法重载的实体类
/// </summary>
public class OverloadedEntity : Entity<OverloadedEntityId>, IAggregateRoot
{
    protected OverloadedEntity()
    {
    }

    public string Name { get; private set; } = string.Empty;
    public int Code { get; private set; }

    // 无参构造函数
    public OverloadedEntity(OverloadedEntityId id)
    {
        Id = id;
    }

    // 带一个参数的构造函数
    public OverloadedEntity(OverloadedEntityId id, string name)
    {
        Id = id;
        Name = name;
    }

    // 带两个参数的构造函数
    public OverloadedEntity(OverloadedEntityId id, string name, int code)
    {
        Id = id;
        Name = name;
        Code = code;
    }

    // 静态方法 - 无参数重载
    public static OverloadedEntity Create()
    {
        return new OverloadedEntity(new OverloadedEntityId(Guid.NewGuid()));
    }

    // 静态方法 - 一个参数重载
    public static OverloadedEntity Create(string name)
    {
        return new OverloadedEntity(new OverloadedEntityId(Guid.NewGuid()), name);
    }

    // 静态方法 - 两个参数重载
    public static OverloadedEntity Create(string name, int code)
    {
        return new OverloadedEntity(new OverloadedEntityId(Guid.NewGuid()), name, code);
    }

    // 实例方法 - 无参数重载
    public void Update()
    {
        AddDomainEvent(new OverloadedEntityUpdatedEvent(this));
    }

    // 实例方法 - 一个参数重载
    public void Update(string name)
    {
        Name = name;
        AddDomainEvent(new OverloadedEntityUpdatedEvent(this));
    }

    // 实例方法 - 两个参数重载
    public void Update(string name, int code)
    {
        Name = name;
        Code = code;
        AddDomainEvent(new OverloadedEntityUpdatedEvent(this));
    }
}

public record OverloadedEntityUpdatedEvent(OverloadedEntity Entity) : IDomainEvent;
