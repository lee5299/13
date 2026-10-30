from pathlib import Path
p=Path(r'D:\13\업무정리\src\WorkOrganizer.Core\Store.cs');s=p.read_text(encoding='utf-8');s=s.replace('public sealed class WorkStore\n{','public sealed class WorkStore\n{\n    private readonly object sync = new();')
methods=[('void','SaveSettings','AppSettings settings','settings'),('List<WorkItem>','Scan','',''),('void','Save','WorkItem item','item'),('void','ChangeState','WorkItem item, WorkState state','item, state'),('string','SaveText','WorkItem item, string text, string expectedHash','item, text, expectedHash'),('string','CopyInto','WorkItem item, string source, string subfolder','item, source, subfolder'),('void','ConnectFile','WorkItem item, Submission slot, string path','item, slot, path'),('void','Rename','WorkItem item, string folderName','item, folderName'),('void','Reconnect','WorkItem item, string folder','item, folder')]
for ret,name,params,args in methods:
 old=f'    public {ret} {name}({params})\n    {{'
 if old not in s:raise Exception(name)
 prefix='return ' if ret!='void' else ''
 wrapper=f'    public {ret} {name}({params}) {{ lock (sync) {{ {prefix}{name}Core({args}); }} }}\n    private {ret} {name}Core({params})\n    {{'
 s=s.replace(old,wrapper,1)
# Creation spans validation, copies and metadata; keep it within the same store lock.
sig='string name, string original, string understood, DateOnly requestDate, decimal effort, int priority, DateOnly? due, WorkType? type = null, string? occurrence = null, string? existingFolder = null'
old=f'    public WorkItem Create({sig})\n    {{';assert old in s
s=s.replace(old,f'    public WorkItem Create({sig}) {{ lock (sync) return CreateCore(name, original, understood, requestDate, effort, priority, due, type, occurrence, existingFolder); }}\n    private WorkItem CreateCore({sig})\n    {{',1)
p.write_text(s,encoding='utf-8')
