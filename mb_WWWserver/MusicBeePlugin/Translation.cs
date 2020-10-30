using System;
using System.Xml.Serialization;

namespace MusicBeePlugin
{
	// Token: 0x0200007C RID: 124
	public class Translation
	{
		// Token: 0x17000009 RID: 9
		// (get) Token: 0x060001C9 RID: 457 RVA: 0x00007D84 File Offset: 0x00005F84
		// (set) Token: 0x060001CA RID: 458 RVA: 0x000022A6 File Offset: 0x000004A6
		[XmlAttribute]
		public string Name { get; set; }

		// Token: 0x1700000A RID: 10
		// (get) Token: 0x060001CB RID: 459 RVA: 0x00007D9C File Offset: 0x00005F9C
		// (set) Token: 0x060001CC RID: 460 RVA: 0x000022AF File Offset: 0x000004AF
		[XmlText]
		public string Value { get; set; }
	}
}
