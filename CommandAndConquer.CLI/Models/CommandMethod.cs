﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CommandAndConquer.CLI.Attributes;

namespace CommandAndConquer.CLI.Models
{
    public class CommandMethod
    {
        public string Name { get; set; }
        public MethodInfo Info { get; set; }
        public MethodParameters Parameters { get; set; }

        public CommandMethod(MethodInfo info)
        {
            Info = info;
            Name = GetCommandName();
        }

        public void Invoke(List<CommandLineArgument> args)
        {
            try
            {
                var paramList = GetParams(args);
                if (paramList == null) return;

                try
                {
                    Info.Invoke(null, BindingFlags.Static, null, paramList, null);
                }
                catch (TargetInvocationException e)
                {
                    throw e.InnerException;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("An error occured while attempting to execute the command.");
                Console.WriteLine("This is most likely due to invalid arguments.");
                Console.WriteLine("Please verify the command usage with '?' and try again.");
            }
        }

        public void SetParameters(List<CommandLineArgument> args)
        {
            var methodParams = new MethodParameters();

            foreach (var argument in args.Where(a => Info.GetParameters().All(p => p.Name != a.Command)))
            {
                methodParams.Errors.Add($"The parameter {argument.Command} is not a valid parameter.");
            }

            foreach (var parameter in Info.GetParameters())
            {
                var wasFound = false;
                foreach (var argument in args)
                {
                    if (argument.Command.ToLower() != parameter.Name.ToLower()) continue;
                    wasFound = true;

                    var type = parameter.ParameterType;

                    if (typeof(IEnumerable).IsAssignableFrom(type) && type.Name != "String")
                    {
                        if (type.GetGenericArguments().Length <= 0)
                        {
                            var underType = type.GetElementType();
                            var listType = typeof(List<>).MakeGenericType(underType);
                            var list = (IList)Activator.CreateInstance(listType);

                            foreach (var value in argument.Values)
                            {
                                list.Add(GetParamValue(value, underType));
                            }

                            Array array = Array.CreateInstance(underType, list.Count);
                            list.CopyTo(array, 0);

                            methodParams.Parameters.Add(array);
                        }
                        else
                        {
                            var underType = type.GenericTypeArguments[0];
                            var listType = typeof(List<>).MakeGenericType(underType);
                            var list = (IList)Activator.CreateInstance(listType);

                            foreach (var value in argument.Values)
                            {
                                list.Add(GetParamValue(value, underType));
                            }

                            methodParams.Parameters.Add(list);
                        }
                    }
                    else
                    {
                        dynamic val = GetParamValue(argument.Values[0], parameter.ParameterType);
                        methodParams.Parameters.Add(val);
                    }
                }

                if (!wasFound)
                {
                    if (parameter.HasDefaultValue)
                    {
                        methodParams.Parameters.Add(parameter.DefaultValue);
                    }
                    else
                    {
                        methodParams.Errors.Add($"The parameter {parameter.Name} must be specified.");
                    }
                }
            }

            Parameters = methodParams;
        }

        public void OutputDocumentation()
        {
            var attrs = Attribute.GetCustomAttributes(Info);
            
            foreach (var attr in attrs)
            {
                if (!(attr is CliCommand)) continue;
                var a = (CliCommand)attr;
            
                Console.WriteLine();
                Console.WriteLine($"{a.Name}");
                Console.WriteLine($"Description: {a.Description}");
                var commandParams = Info.GetParameters();
                if (commandParams.Length > 0)
                {
                    Console.WriteLine($"Parameters:");
                    foreach (var cp in commandParams)
                    {
                        OutputParameterDocumentation(cp);
                    }
                }
            }
        }

        private dynamic GetParamValue(string value, Type type)
        {
            if (Nullable.GetUnderlyingType(type) != null)
            {
                var underType = Nullable.GetUnderlyingType(type);
                dynamic val = Convert.ChangeType(value, underType);
                return val;
            }

            if (type.IsEnum)
            {
                return Enum.Parse(type, value);
            }

            dynamic pVal = Convert.ChangeType(value, type);
            return pVal;
        }

        private object[] GetParams(List<CommandLineArgument> args)
        {
            SetParameters(args);

            foreach (var error in Parameters.Errors)
            {
                Console.WriteLine(error);
            }

            return Parameters.Errors.Any() ? null : Parameters.Parameters.ToArray();
        }

        private void OutputParameterDocumentation(ParameterInfo cp)
        {
            var priorityString = cp.HasDefaultValue ? "Optional" : "Required";
            var type = Nullable.GetUnderlyingType(cp.ParameterType) ?? cp.ParameterType;
        
            if (type.IsEnum)
            {
                var names = type.GetEnumNames();
                Console.WriteLine($"-{cp.Name} (string): This parameter is {priorityString} and must be one of these following ({string.Join(",", names)}).");
            }
            else
            {
                var typeName = type.Name;
                Console.WriteLine($"-{cp.Name} ({typeName}): This parameter is {priorityString}.");
            }
        }

        private string GetCommandName()
        {
            var attribute = (CliCommand)Attribute.GetCustomAttributes(Info)
                .FirstOrDefault(a => a is CliCommand);

            return attribute?.Name;
        }
    }
}