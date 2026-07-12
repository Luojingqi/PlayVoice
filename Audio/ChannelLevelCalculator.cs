using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Text;

namespace PlayVoice.Audio;

public class ChannelLevelCalculator
{
    private readonly ISampleProvider sourceProvider;
    private readonly int channelCount;
    private readonly float[] buffer;

    // 用于存储计算出的电平值，范围 0.0 到 1.0
    public float LeftChannelLevel { get; private set; }
    public float RightChannelLevel { get; private set; }

    public ChannelLevelCalculator(ISampleProvider sourceProvider)
    {
        this.sourceProvider = sourceProvider;
        this.channelCount = sourceProvider.WaveFormat.Channels;
        // 缓冲区大小可以按需调整，例如每次处理 1024 个样本
        this.buffer = new float[1024 * channelCount];
    }

    // 此方法需要在音频播放循环中被持续调用，例如在事件或定时器中
    public void Update()
    {
        // 1. 从音频源读取样本数据到缓冲区
        int samplesRead = sourceProvider.Read(buffer, 0, buffer.Length);

        if (samplesRead == 0)
        {
            // 如果没有读取到数据，可将电平归零或保持上次的值
            LeftChannelLevel = 0;
            RightChannelLevel = 0;
            return;
        }

        // 2. 初始化用于计算峰值的变量
        float maxLeft = 0;
        float maxRight = 0;

        // 3. 遍历缓冲区，分离并计算每个声道的峰值
        for (int i = 0; i < samplesRead; i += channelCount)
        {
            // 获取当前采样点的各个声道样本值 (范围 -1.0 到 1.0)
            float leftSample = buffer[i];
            // 如果是立体声，右声道样本在下一个索引
            float rightSample = channelCount == 2 ? buffer[i + 1] : leftSample;

            // 取绝对值并比较，更新峰值
            maxLeft = Math.Max(maxLeft, Math.Abs(leftSample));
            maxRight = Math.Max(maxRight, Math.Abs(rightSample));
        }

        // 4. 更新公开的属性，供 UI 绑定使用
        LeftChannelLevel = maxLeft;
        RightChannelLevel = maxRight;
    }
}