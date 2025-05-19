using System;
using System.Collections.Generic;
using MapperLibrary;

namespace TestMapper
{
    // Source models
    public class SourcePerson
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public DateTime Birthday { get; set; }
        public SourceAddress Address { get; set; }
        public List<SourceSkill> Skills { get; set; }
        public SourceStatus Status { get; set; }
    }

    public class SourceAddress
    {
        public string Street { get; set; }
        public string City { get; set; }
        public string ZipCode { get; set; }
    }

    public class SourceSkill
    {
        public string Name { get; set; }
        public int YearsOfExperience { get; set; }
    }

    public enum SourceStatus
    {
        Active = 1,
        Inactive = 2,
        Pending = 3
    }

    // Target models
    public class TargetPerson
    {
        public int Id { get; set; }
        public string FullName { get; set; }  // Different property name
        public DateTime DateOfBirth { get; set; }  // Different property name
        public TargetAddress HomeAddress { get; set; }  // Different property name
        public List<TargetSkill> Abilities { get; set; }  // Different property name
        public TargetStatus UserStatus { get; set; }  // Different property name
    }

    public class TargetAddress
    {
        public string Street { get; set; }
        public string City { get; set; }
        public string PostalCode { get; set; }  // Different property name
    }

    public class TargetSkill
    {
        public string Title { get; set; }  // Different property name
        public int Experience { get; set; }  // Different property name
    }

    public enum TargetStatus
    {
        Active = 1,
        Inactive = 2,
        Pending = 3
    }

    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Mapper Example");
            Console.WriteLine("=============");

            // Create source data
            var sourcePerson = new SourcePerson
            {
                Id = 1,
                Name = "John Doe",
                Birthday = new DateTime(1985, 5, 15),
                Address = new SourceAddress
                {
                    Street = "123 Main St",
                    City = "New York",
                    ZipCode = "10001"
                },
                Skills = new List<SourceSkill>
                {
                    new SourceSkill { Name = "Programming", YearsOfExperience = 5 },
                    new SourceSkill { Name = "Design", YearsOfExperience = 3 }
                },
                Status = SourceStatus.Active
            };

            // Create mapper
            var mapper = new Mapper();            // Configure custom property mappings
            mapper.CreateMap<SourcePerson, TargetPerson>()
                .ForMember(src => src.Name, dest => dest.FullName)
                .ForMember(src => src.Birthday, dest => dest.DateOfBirth)
                .ForMember(src => src.Address, dest => dest.HomeAddress)
                .ForMember(src => src.Skills, dest => dest.Abilities)
                .ForMember(src => src.Status, dest => dest.UserStatus);

            mapper.CreateMap<SourceAddress, TargetAddress>()
                .ForMember(src => src.ZipCode, dest => dest.PostalCode);

            mapper.CreateMap<SourceSkill, TargetSkill>()
                .ForMember(src => src.Name, dest => dest.Title)
                .ForMember(src => src.YearsOfExperience, dest => dest.Experience);
                
            // Demonstrate ignoring a property by creating a new mapper
            var ignoringMapper = new Mapper();
            ignoringMapper.CreateMap<SourcePerson, TargetPerson>()
                .ForMember(src => src.Name, dest => dest.FullName)
                .ForMember(src => src.Birthday, dest => dest.DateOfBirth)
                .ForMember(src => src.Address, dest => dest.HomeAddress)
                .ForMember(src => src.Skills, dest => dest.Abilities)
                .ForMember(src => src.Status, dest => dest.UserStatus)
                .Ignore(dest => dest.FullName);  // Ignore the FullName property

            // Perform mapping
            var targetPerson = mapper.Map<SourcePerson, TargetPerson>(sourcePerson);

            // Display results
            Console.WriteLine("\nSource Person:");
            Console.WriteLine($"Id: {sourcePerson.Id}");
            Console.WriteLine($"Name: {sourcePerson.Name}");
            Console.WriteLine($"Birthday: {sourcePerson.Birthday:yyyy-MM-dd}");
            Console.WriteLine($"Status: {sourcePerson.Status}");
            Console.WriteLine("Address:");
            Console.WriteLine($"  Street: {sourcePerson.Address.Street}");
            Console.WriteLine($"  City: {sourcePerson.Address.City}");
            Console.WriteLine($"  ZipCode: {sourcePerson.Address.ZipCode}");
            Console.WriteLine("Skills:");
            foreach (var skill in sourcePerson.Skills)
            {
                Console.WriteLine($"  {skill.Name} - {skill.YearsOfExperience} years");
            }

            Console.WriteLine("\nTarget Person:");
            Console.WriteLine($"Id: {targetPerson.Id}");
            Console.WriteLine($"FullName: {targetPerson.FullName}");
            Console.WriteLine($"DateOfBirth: {targetPerson.DateOfBirth:yyyy-MM-dd}");
            Console.WriteLine($"UserStatus: {targetPerson.UserStatus}");
            Console.WriteLine("HomeAddress:");
            Console.WriteLine($"  Street: {targetPerson.HomeAddress.Street}");
            Console.WriteLine($"  City: {targetPerson.HomeAddress.City}");
            Console.WriteLine($"  PostalCode: {targetPerson.HomeAddress.PostalCode}");
            Console.WriteLine("Abilities:");
            foreach (var ability in targetPerson.Abilities)
            {
                Console.WriteLine($"  {ability.Title} - {ability.Experience} years");
            }            // Test list mapping
            var sourcePersonList = new List<SourcePerson> { sourcePerson, sourcePerson };
            var targetPersonList = mapper.MapCollection<SourcePerson, TargetPerson>(sourcePersonList).ToList();
            
            Console.WriteLine($"\nSuccessfully mapped {targetPersonList.Count} persons in collection");
            
            // Test ignore functionality
            Console.WriteLine("\nTesting property ignoring:");
            var normalTarget = mapper.Map<SourcePerson, TargetPerson>(sourcePerson);
            var ignoredTarget = ignoringMapper.Map<SourcePerson, TargetPerson>(sourcePerson);
            
            Console.WriteLine($"Normal mapping - FullName: '{normalTarget.FullName}'");
            Console.WriteLine($"With ignore - FullName: '{ignoredTarget.FullName}' (should be null or empty)");
        }
    }
}
