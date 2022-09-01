using System;
using System.Globalization;

namespace OpenTap
{
    class DateTimeAnnotation : IStringValueAnnotation, ICopyStringValueAnnotation
    {
        private AnnotationCollection annotation;

        public string Value
        {
            get
            {
                if (annotation.Get<IObjectValueAnnotation>(from: this).Value is DateTime dt)
                    return dt.ToString(CultureInfo.CurrentCulture);
                return "";
            }
            set
            {
                annotation.Get<IObjectValueAnnotation>(from: this).Value = DateTime.Parse(value);
            }
        }

        public DateTimeAnnotation(AnnotationCollection annotation)
        {
            this.annotation = annotation;
        }
    }
}